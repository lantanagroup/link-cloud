using Confluent.Kafka.Extensions.OpenTelemetry;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Telemetry
{
    public static class TelemetryServiceExtension
    {
        public static IServiceCollection AddOpenTelemetryService(this IServiceCollection services, Action<TelemetryServiceOptions> options)
        {
            var telemetryServiceOptions = new TelemetryServiceOptions();
            options.Invoke(telemetryServiceOptions);

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
                        .AddHttpClientInstrumentation(options =>
                        {
                            options.FilterHttpRequestMessage = (httpContext) =>
                            {
                                return !(httpContext.RequestUri is not null
                                    && (
                                        httpContext.RequestUri.PathAndQuery.StartsWith("/loki") ||
                                        httpContext.RequestUri.PathAndQuery.Contains("swagger") ||
                                        httpContext.RequestUri.PathAndQuery.StartsWith("/health")
                                    ));
                            };
                        })
                        .AddSource("Yarp.ReverseProxy")
                        .AddConfluentKafkaInstrumentation()
                        .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryServiceOptions.TelemetryCollectorEndpoint); }));

            otel.WithMetrics(metricsProviderBuilder =>
                    metricsProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddProcessInstrumentation()
                        .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryServiceOptions.TelemetryCollectorEndpoint); }));

            if (telemetryServiceOptions.EnableRuntimeInstrumentation)
            {
                otel.WithMetrics(metricsProviderBuilder =>
                    metricsProviderBuilder
                        .AddRuntimeInstrumentation());
            }

            if (telemetryServiceOptions.Environment.IsDevelopment())
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

    public class TelemetryServiceOptions : TelemetryConfig
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
    }
}
