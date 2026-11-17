using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Sales.Tests;

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Supplied_csv_returns_exactly_the_six_requested_fields()
    {
        using var client = factory.CreateHttpsClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Input", "SalesOrders.csv"))),
            "file", "SalesOrders.csv");
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.Equal(6, body.EnumerateObject().Count());
        Assert.Equal("GBC DocuBind P400 Electric Binding System", body.GetProperty("leastProfitableProduct").GetString());
        Assert.Equal("Avery Hidden Tab Dividers for Binding Systems", body.GetProperty("productWithHighestAverageDiscount").GetString());
        Assert.Equal(.8m, body.GetProperty("highestAverageDiscount").GetDecimal());
        Assert.Equal("Advantus Push Pins", body.GetProperty("productWithMostReturnedOrders").GetString());
        Assert.Equal("Kansas", body.GetProperty("stateWithLowestTotalSales").GetString());
        Assert.Equal(726.2126422410771, body.GetProperty("salesStandardDeviation").GetDouble(), 8);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Product Name,State,Sales,Profit,Discount,Returned\nA,CA,invalid,0,0,Not")]
    [InlineData("Product Name,State,Sales,Profit,Discount,Returned")]
    public async Task Invalid_file_returns_400_problem_details(string csv)
    {
        using var client = factory.CreateHttpsClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "invalid.csv");
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Invalid sales file", problem.Title);
    }

    [Fact]
    public async Task Missing_file_returns_400()
    {
        using var client = factory.CreateHttpsClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("no upload"), "description");
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_text_encoding_returns_400()
    {
        using var client = factory.CreateHttpsClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0xC3, 0x28]), "file", "invalid.csv");
        using var response = await client.PostAsync("/api/sales/analysis", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_content_type_returns_415()
    {
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync("/api/sales/analysis", new { file = "not a file" });
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
