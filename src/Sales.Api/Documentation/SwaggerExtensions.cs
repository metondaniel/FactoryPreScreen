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
                Description = "Upload one CSV (maximum 5 MiB) to calculate the six sales metrics. Use Authorize to provide X-API-Key."
            });
            options.AddSecurityDefinition(ApiKeyAuthenticationOptions.SchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = ApiKeyAuthenticationOptions.HeaderName,
                Description = "Enter the API key configured through user-secrets or Security__ApiKey."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = ApiKeyAuthenticationOptions.SchemeName
                    }
                }] = Array.Empty<string>()
            });
        });
        return services;
    }
}
