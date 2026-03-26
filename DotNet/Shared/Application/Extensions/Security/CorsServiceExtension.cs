using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Extensions.Security
{
    public static class CorsServiceExtension
    {
        public static IServiceCollection AddLinkCorsService(this IServiceCollection services, Action<CorsServiceOptions>? options = null)
        {
            var corsServiceOptions = new CorsServiceOptions();
            options?.Invoke(corsServiceOptions);

            var corsSettings = services.BuildServiceProvider().GetService<IOptions<CorsSettings>>();

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

                //determine allowed origins
                if (!corsServiceOptions.AllowAllOrigins)
                {                    
                    cpb.WithOrigins(corsServiceOptions.AllowedOrigins ?? []);                   
                }
                else
                {
                    cpb.SetIsOriginAllowed((Host) => true);                   
                }

                //determine allowed headers
                if(corsServiceOptions.AllowAllHeaders)
                {
                    cpb.AllowAnyHeader();
                }
                else
                {
                    cpb.WithHeaders(corsServiceOptions.AllowedHeaders is not null ? corsServiceOptions.AllowedHeaders : corsServiceOptions.DefaultAllowedHeaders);
                }

                //determine allowed methods
                if(corsServiceOptions.AllowAllMethods)
                {
                    cpb.AllowAnyMethod();
                }
                else
                {
                    cpb.WithMethods(corsServiceOptions.AllowedMethods is not null ? corsServiceOptions.AllowedMethods : corsServiceOptions.DefaultAllowedMethods);
                }

                //determine if credentials are allowed
                if(corsServiceOptions.AllowCredentials)
                {
                    cpb.AllowCredentials();
                }
                
                cpb.WithExposedHeaders(corsServiceOptions.AllowedExposedHeaders is not null ? corsServiceOptions.AllowedExposedHeaders : corsServiceOptions.DefaultAllowedExposedHeaders);
                cpb.SetPreflightMaxAge(TimeSpan.FromSeconds(corsServiceOptions.MaxAge));

                options.AddPolicy(CorsSettings.DefaultCorsPolicyName, cpb.Build());

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
