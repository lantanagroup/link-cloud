using Confluent.Kafka.Extensions.OpenTelemetry;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.IdentityModel.Tokens.Jwt;

namespace LantanaGroup.Link.Notification.Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddOpenTelemetryService(this IServiceCollection services, TelemetryConfig telemetryConfig, IWebHostEnvironment env)
        {
            var otel = services.AddOpenTelemetry();

            //configure OpenTelemetry resources with application name
            otel.ConfigureResource(resource => resource
                .AddService(
                    serviceName: ServiceActivitySource.Instance.Name,
                    serviceVersion: ServiceActivitySource.Instance.Version
                ));

            otel.WithTracing(tracerProviderBuilder =>
                    tracerProviderBuilder
                        .AddSource(ServiceActivitySource.Instance.Name)
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.Filter = (httpContext) => httpContext.Request.Path != "/health"; //do not capture traces for the health check endpoint
                            options.Filter = (httpContext) => httpContext.Request.Path.ToString().Contains("/swagger"); //do not capture traces for the swagger endpoint
                        })
                        .AddConfluentKafkaInstrumentation()
                        .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

            otel.WithMetrics(metricsProviderBuilder =>
                    metricsProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddProcessInstrumentation()
                        .AddMeter("LinkNotificationService")
                        .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

            if (telemetryConfig.EnableRuntimeInstrumentation)
            {
                otel.WithMetrics(metricsProviderBuilder =>
                    metricsProviderBuilder
                        .AddRuntimeInstrumentation());
            }

            if (env.IsDevelopment())
            {
                otel.WithTracing(tracerProviderBuilder =>
                    tracerProviderBuilder
                     .AddConsoleExporter());

                //metrics are very verbose, only enable console exporter if you really want to see metric details
                //otel.WithMetrics(metricsProviderBuilder =>
                //    metricsProviderBuilder
                //        .AddConsoleExporter());                
            }

            services.AddSingleton<NotificationServiceMetrics>();

            return services;
        }

        public static IServiceCollection AddCorsService(this IServiceCollection services, IWebHostEnvironment env)
        {
            //TODO: Use env variable to control strictness of CORS policy
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowed((host) => true) //lock this down, allows all atm
                        .AllowAnyHeader());
            });

            return services;
        }

        public static IServiceCollection AddAuthenticationService(this IServiceCollection services, IdentityProviderConfig idpConfig, IWebHostEnvironment env)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = idpConfig.Issuer; //gets the IDP metadata about endpoints and keys
                options.Audience = idpConfig.Audience;
                if (env.IsDevelopment())
                {
                    options.RequireHttpsMetadata = false;
                }
                options.TokenValidationParameters = new()
                {
                    NameClaimType = idpConfig.NameClaimType,
                    RoleClaimType = idpConfig.RoleClaimType,
                    ValidTypes = idpConfig.ValidTypes //avoid jwt confusion attacks (ie: circumvent token signature checking)
                };
            });

            return services;
        }

        public static IServiceCollection AddAuthorizationService(this IServiceCollection services)
        {
            //services.AddAuthorization(authorizationOptions =>
            //{
            //    authorizationOptions.AddPolicy("UserCanViewAuditLogs", AuthorizationPolicies.CanViewAuditLogs());
            //    authorizationOptions.AddPolicy("CanCreateAuditLogs", AuthorizationPolicies.CanCreateAuditLogs());

            //    authorizationOptions.AddPolicy("ClientApplicationCanRead", policyBuilder =>
            //    {
            //        policyBuilder.RequireScope("botwdemogatewayapi.read");
            //    });

            //    authorizationOptions.AddPolicy("ClientApplicationCanCreate", policyBuilder =>
            //    {
            //        policyBuilder.RequireScope("botwdemogatewayapi.write");
            //    });

            //    authorizationOptions.AddPolicy("ClientApplicationCanDelete", policyBuilder =>
            //    {
            //        policyBuilder.RequireScope("botwdemogatewayapi.delete");
            //    });

            //});

            return services;
        }
    }
}
