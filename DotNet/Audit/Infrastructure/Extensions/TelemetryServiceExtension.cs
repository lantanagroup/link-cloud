using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Confluent.Kafka.Extensions.OpenTelemetry;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LantanaGroup.Link.Audit.Infrastructure.Extensions
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

            //Add Tracing if enabled
            if (telemetryServiceOptions.EnableTracing)
            {
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
                        .AddConfluentKafkaInstrumentation());

                //Configure Exporters
                if (telemetryServiceOptions.EnableOtelCollector)
                {
                    otel.WithTracing(exportOptions =>
                    {
                        if (string.IsNullOrEmpty(telemetryServiceOptions.OtelCollectorEndpoint)) { throw new NullReferenceException("No OTEL collector endpoint was found."); }
                        exportOptions.AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryServiceOptions.OtelCollectorEndpoint); });
                    });
                }
            }          

            //Add metrics if enabled
            if (telemetryServiceOptions.EnableMetrics)
            {
                otel.WithMetrics(metricsProviderBuilder =>
                    metricsProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddProcessInstrumentation());

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

                //Configure Exporters
                if (telemetryServiceOptions.EnableOtelCollector)
                {
                    otel.WithMetrics(exportOptions =>
                    {
                        if (string.IsNullOrEmpty(telemetryServiceOptions.OtelCollectorEndpoint)) { throw new NullReferenceException("No OTEL collector endpoint was found."); }
                        exportOptions.AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryServiceOptions.OtelCollectorEndpoint); });
                    });
                }               
            }

            //Add Azure Monitor if enabled
            if (telemetryServiceOptions.EnableAzureMonitor)
            {               
                otel.UseAzureMonitor(options => 
                {
                    options.Credential = new DefaultAzureCredential();
                    options.ConnectionString = telemetryServiceOptions.AzureMonitorConnectionString;
                });
            }

            return services;
        }

        public class TelemetryServiceOptions : TelemetryConfig
        {
            public IWebHostEnvironment Environment { get; set; } = null!;          
        }
    }
}
