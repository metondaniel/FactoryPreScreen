using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sales.Api.Security;
using Sales.Application;
using Sales.Domain;

namespace Sales.Tests;

public sealed class SecurityTests
{
    private const string Csv = "Product Name,State,Sales,Profit,Discount,Returned\nA,CA,1,0,0,Not";

    [Fact]
    public async Task Swagger_exposes_file_upload_without_authorization_in_development()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var document = json.RootElement;
        Assert.False(document.TryGetProperty("security", out _));
        if (document.TryGetProperty("components", out var components))
            Assert.False(components.TryGetProperty("securitySchemes", out _));
        var operation = document.GetProperty("paths").GetProperty("/api/sales/analysis").GetProperty("post");
        var file = operation.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data")
            .GetProperty("schema").GetProperty("properties").GetProperty("file");
        Assert.Equal("binary", file.GetProperty("format").GetString());
        Assert.False(operation.GetProperty("responses").TryGetProperty("401", out _));
        var ui = await client.GetStringAsync("/swagger/index.html");
        Assert.Contains("swagger-ui", ui);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task Swagger_is_unavailable_in_production(string path)
    {
        using var factory = new ApiFactory { EnvironmentName = "Production" };
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Production_rejects_plain_http_and_untrusted_forwarded_proto()
    {
        using var factory = new ApiFactory { EnvironmentName = "Production" };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://example.test"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        using var form = Upload();
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("HTTPS required", problem!.Title);
    }

    [Fact]
    public async Task Production_https_succeeds_and_sets_security_headers()
    {
        using var factory = new ApiFactory { EnvironmentName = "Production" };
        using var client = factory.CreateHttpsClient();
        using var form = Upload();
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData(UploadLimits.MaxFileBytes + 1)]
    [InlineData(UploadLimits.MaxRequestBytes + 1)]
    public async Task Oversized_file_or_request_returns_413(int length)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[length]), "file", "large.csv");
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Multiple_files_are_rejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var form = Upload();
        form.Add(new StringContent(Csv), "other", "second.csv");
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("file.exe")]
    [InlineData("file.csv.exe")]
    public async Task Unsupported_extensions_are_rejected(string name)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var form = Upload(name);
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_rejects_the_thirty_first_request()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var form = Upload();
            using var response = await client.PostAsync("/api/sales/analysis", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var rejectedForm = Upload();
        using var rejected = await client.PostAsync("/api/sales/analysis", rejectedForm);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }

    [Fact]
    public async Task Unexpected_errors_do_not_expose_internal_details()
    {
        using var baseFactory = new ApiFactory { EnvironmentName = "Production" };
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISalesRecordReader>();
            services.AddSingleton<ISalesRecordReader, FailingReader>();
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://example.test") });
        using var form = Upload();
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive-test-marker", body);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task At_most_two_uploads_are_processed_concurrently()
    {
        using var reader = new BlockingReader();
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISalesRecordReader>();
            services.AddSingleton<ISalesRecordReader>(reader);
        }));
        using var client = factory.CreateClient();
        using var firstForm = Upload();
        using var secondForm = Upload();
        var first = Task.Run(() => client.PostAsync("/api/sales/analysis", firstForm));
        var second = Task.Run(() => client.PostAsync("/api/sales/analysis", secondForm));
        try
        {
            Assert.True(reader.Entered.Wait(TimeSpan.FromSeconds(10)));
            using var thirdForm = Upload();
            using var response = await client.PostAsync("/api/sales/analysis", thirdForm);
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        finally { reader.Release.Set(); }
        using var firstResponse = await first;
        using var secondResponse = await second;
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    private static MultipartFormDataContent Upload(string name = "sales.csv")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(Csv)), "file", name);
        return form;
    }

    private sealed class FailingReader : ISalesRecordReader
    {
        public IEnumerable<SalesRecord> Read(Stream stream) => throw new InvalidOperationException("sensitive-test-marker");
    }

    private sealed class BlockingReader : ISalesRecordReader, IDisposable
    {
        public CountdownEvent Entered { get; } = new(2);
        public ManualResetEventSlim Release { get; } = new();
        public IEnumerable<SalesRecord> Read(Stream stream)
        {
            Entered.Signal();
            if (!Release.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("The concurrency test did not release the reader.");
            return [new("A", "CA", 1, 0, 0, false)];
        }
        public void Dispose() { Entered.Dispose(); Release.Dispose(); }
    }
}
