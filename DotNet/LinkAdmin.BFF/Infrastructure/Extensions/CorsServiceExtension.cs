﻿using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class CorsServiceExtension
    {
        public static IServiceCollection AddCorsService(this IServiceCollection services, Action<CorsServiceOptions>? options = null)
        {
            var corsServiceOptions = new CorsServiceOptions();
            options?.Invoke(corsServiceOptions);
            
            services.AddCors(options =>
            {                
                CorsPolicyBuilder cpb = new();

                if(corsServiceOptions.AllowedOrigins?.Length > 0)
                {
                    cpb.WithOrigins(corsServiceOptions.AllowedOrigins);

                    if(corsServiceOptions.AllowCredentials)
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
                    
                options.AddPolicy(corsServiceOptions?.PolicyName ?? CorsConfig.DefaultCorsPolicyName, cpb.Build());

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
