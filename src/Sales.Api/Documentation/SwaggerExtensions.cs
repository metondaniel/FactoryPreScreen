using Microsoft.OpenApi.Models;
using Sales.Api.Security;

namespace Sales.Api.Documentation;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSalesSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Sales Analysis API",
                Version = "v1",
                Description = "Upload one CSV (maximum 5 MiB) to calculate the six sales metrics."
            });
        });
        return services;
    }
}
