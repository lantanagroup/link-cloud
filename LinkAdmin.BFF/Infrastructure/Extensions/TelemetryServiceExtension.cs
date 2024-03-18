using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class TelemetryServiceExtension
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
                        })
                        .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

            otel.WithMetrics(metricsProviderBuilder =>
                    metricsProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddProcessInstrumentation()
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

            return services;
        }     
        
    }
}
