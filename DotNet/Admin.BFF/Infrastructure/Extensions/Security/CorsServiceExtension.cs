using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class CorsServiceExtension
    {
        public static IServiceCollection AddCorsService(this IServiceCollection services, Action<CorsServiceOptions>? options = null, Serilog.ILogger? logger = null)
        {
            var corsServiceOptions = new CorsServiceOptions();
            options?.Invoke(corsServiceOptions);

            services.AddCors(options =>
            {
                CorsPolicyBuilder cpb = new();

                if (corsServiceOptions.AllowedOrigins?.Length > 0 && corsServiceOptions.AllowAllOrigins is false)
                {
                    logger?.Debug("CORS -- Allowing origins: {AllowedOrigins}", corsServiceOptions.AllowedOrigins);
                    cpb.WithOrigins(corsServiceOptions.AllowedOrigins);

                    if (corsServiceOptions.AllowCredentials)
                    {
                        cpb.AllowCredentials();
                        cpb.WithHeaders(corsServiceOptions.AllowedHeaders ?? corsServiceOptions.DefaultAllowedHeaders);
                    }
                }
                else
                {
                    logger?.Debug("CORS -- Allowing all origins");
                    cpb.SetIsOriginAllowed((Host) => true);

                    if (corsServiceOptions.AllowedHeaders?.Length > 0)
                    {
                        logger?.Debug("CORS -- Allowing headers: {AllowedHeaders}", corsServiceOptions.AllowedHeaders);
                        cpb.WithHeaders(corsServiceOptions.AllowedHeaders);
                    }
                    else
                    {
                        logger?.Debug("CORS -- Allowing all headers");
                        cpb.AllowAnyHeader();
                    }

                }

                cpb.WithMethods(corsServiceOptions.AllowedMethods ?? corsServiceOptions.DefaultAllowedMethods);
                cpb.WithExposedHeaders(corsServiceOptions.AllowedExposedHeaders ?? corsServiceOptions.DefaultAllowedExposedHeaders);
                cpb.SetPreflightMaxAge(TimeSpan.FromSeconds(corsServiceOptions.MaxAge));

                options.AddPolicy(CorsConfig.DefaultCorsPolicyName, cpb.Build());

                //add health check endpoint to cors policy
                options.AddPolicy("HealthCheckPolicy", policy =>
                {
                    policy.AllowAnyHeader();
                    policy.AllowAnyMethod();
                    policy.AllowAnyOrigin();
                });
            });

            return services;
        }
    }

    public class CorsServiceOptions : CorsConfig
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
    }
}
