using Microsoft.OpenApi.Models;

namespace IB.WatchCluster.Api.Infrastructure
{
    public static class AddSwaggerExtension
    {
        public static IServiceCollection AddSwagger(this IServiceCollection services, string authScheme, string authTokenName)
        {
            return services
                .AddEndpointsApiExplorer()
                .AddSwaggerGen(option =>
                {
                    option.SwaggerDoc("v1", new OpenApiInfo { Title = "Watch Cluster API", Version = "v1" });
                    option.AddSecurityDefinition(authScheme, new OpenApiSecurityScheme
                    {
                        In = ParameterLocation.Query,
                        Description = "Please enter a valid token",
                        Name = authTokenName,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = authScheme
                    });
                    option.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = authScheme
                                }
                            },
                            Array.Empty<string>()
                        }
                    });
                });
        }
    }
}
