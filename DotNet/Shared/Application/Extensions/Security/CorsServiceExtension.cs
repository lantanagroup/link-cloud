using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Extensions.Security
{
    public static class CorsServiceExtension
    {
        public static IServiceCollection AddLinkCorsService(this IServiceCollection services, IOptions<CorsSettings> corsSettings, Action<CorsServiceOptions>? options = null)
        {
            var corsServiceOptions = new CorsServiceOptions();
            options?.Invoke(corsServiceOptions);            

            if(corsSettings is not null && corsSettings.Value.EnableCors)
            {
                services.AddCorsService(options =>
                {
                    options.Environment = corsServiceOptions.Environment;
                    options.AllowAllOrigins = corsSettings.Value.AllowAllOrigins;
                    options.AllowedOrigins = corsSettings.Value.AllowedOrigins;
                    options.AllowCredentials = corsSettings.Value.AllowCredentials;
                    options.AllowedHeaders = corsSettings.Value.AllowedHeaders;
                    options.AllowedMethods = corsSettings.Value.AllowedMethods;
                    options.AllowedExposedHeaders = corsSettings.Value.AllowedExposedHeaders;
                    options.MaxAge = corsSettings.Value.MaxAge;
                    options.PolicyName = corsSettings.Value.PolicyName;
                });
            }            

            return services;
        }

        public static IServiceCollection AddCorsService(this IServiceCollection services, Action<CorsServiceOptions>? options = null)
        {
            var corsServiceOptions = new CorsServiceOptions();
            options?.Invoke(corsServiceOptions);

            services.AddCors(options =>
            {
                CorsPolicyBuilder cpb = new();

                if (!corsServiceOptions.AllowAllOrigins)
                {                    
                    cpb.WithOrigins(corsServiceOptions.AllowedOrigins ?? []);

                    if (corsServiceOptions.AllowCredentials)
                    {
                        cpb.AllowCredentials();
                        cpb.WithHeaders(corsServiceOptions.AllowedHeaders is not null ? corsServiceOptions.AllowedHeaders : corsServiceOptions.DefaultAllowedHeaders);
                    }
                }
                else
                {
                    cpb.SetIsOriginAllowed((Host) => true);

                    if (corsServiceOptions.AllowedHeaders?.Length > 0)
                    {
                        cpb.WithHeaders(corsServiceOptions.AllowedHeaders);
                    }
                    else
                    {
                        cpb.AllowAnyHeader();
                    }

                }

                cpb.WithMethods(corsServiceOptions.AllowedMethods is not null ? corsServiceOptions.AllowedMethods : corsServiceOptions.DefaultAllowedMethods);
                cpb.WithExposedHeaders(corsServiceOptions.AllowedExposedHeaders is not null ? corsServiceOptions.AllowedExposedHeaders : corsServiceOptions.DefaultAllowedExposedHeaders);
                cpb.SetPreflightMaxAge(TimeSpan.FromSeconds(corsServiceOptions.MaxAge));

                options.AddPolicy(corsServiceOptions?.PolicyName ?? CorsSettings.DefaultCorsPolicyName, cpb.Build());

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

    public class CorsServiceOptions : CorsSettings
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
    }
}
