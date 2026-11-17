using Sales.Api;
using Sales.Api.Documentation;
using Sales.Api.Security;
using Sales.Application;
using Sales.Domain;
using Sales.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = UploadLimits.MaxRequestBytes;
});
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = UploadLimits.MaxRequestBytes);
builder.Services.AddUploadProtection();
builder.Services.AddSalesSwagger();
builder.Services.AddScoped<ISalesRecordReader, CsvSalesRecordReader>();
builder.Services.AddScoped<SalesAnalyzer>();
builder.Services.AddScoped<AnalyzeSalesFile>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<RequestProtectionMiddleware>();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Sales Analysis API v1");
    });
}
app.UseRouting();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program;
