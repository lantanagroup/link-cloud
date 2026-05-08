using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Confluent.Kafka.Extensions.OpenTelemetry;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LantanaGroup.Link.Shared.Application.Extensions
{
    public static class TelemetryServiceExtension
    {
        /// <summary>
        /// Convenience method to add OpenTelemetry services to the service
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public static IServiceCollection AddLinkTelemetry(this IServiceCollection services, IConfiguration configuration, Action<TelemetryServiceOptions> options)
        {
            var initTelemetryOptions = new TelemetryServiceOptions();
            options.Invoke(initTelemetryOptions);

            var telemetryConfig = configuration.GetSection(ConfigurationConstants.AppSettings.Telemetry).Get<TelemetrySettings>();

            if (telemetryConfig is not null && telemetryConfig.EnableTelemetry)
            {
                //ensure required telemetry options are provided
                if (initTelemetryOptions.Environment is null || string.IsNullOrEmpty(initTelemetryOptions.ServiceName))
                { 
                    throw new NullReferenceException("Environment and ServiceName must be provided to configure telemetry.");
                }
                
                services.AddOpenTelemetryService(options => {
                    options.Environment = initTelemetryOptions.Environment;
                    options.ServiceName = initTelemetryOptions.ServiceName;
                    options.ServiceVersion = initTelemetryOptions.ServiceVersion;
                    options.EnableTracing = telemetryConfig.EnableTracing;
                    options.EnableMetrics = telemetryConfig.EnableMetrics;
                    options.MeterName = $"Link.{initTelemetryOptions.ServiceName}";
                    options.EnableRuntimeInstrumentation = telemetryConfig.EnableRuntimeInstrumentation;
                    options.InstrumentEntityFramework = telemetryConfig.InstrumentEntityFramework;

                    //configure OTEL Collector
                    options.EnableOtelCollector = telemetryConfig.EnableOtelCollector;
                    options.OtelCollectorEndpoint = telemetryConfig.EnableOtelCollector ?
                        telemetryConfig.OtelCollectorEndpoint : null;

                    //configure Azure Monitor
                    options.EnableAzureMonitor = telemetryConfig.EnableAzureMonitor;
                    options.AzureMonitorConnectionString = telemetryConfig.EnableAzureMonitor ?
                        configuration.GetConnectionString("AzureMonitor") : null;
                });
            }           

            return services;
        }

        /// <summary>
        /// Configure open telemetry for the requesting service
        /// </summary>
        /// <param name="services"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public static IServiceCollection AddOpenTelemetryService(this IServiceCollection services, Action<TelemetryServiceOptions> options)
        {
            var telemetryServiceOptions = new TelemetryServiceOptions();
            options.Invoke(telemetryServiceOptions);

            var otel = services.AddOpenTelemetry();

            //configure OpenTelemetry resources with application name
            otel.ConfigureResource(resource => resource
                .AddService(
                    serviceName: telemetryServiceOptions.ServiceName,
                    serviceVersion: telemetryServiceOptions.ServiceVersion
                ));

            //Add Tracing if enabled
            if (telemetryServiceOptions.EnableTracing)
            {
                otel.WithTracing(tracerProviderBuilder =>
                    tracerProviderBuilder
                        .AddSource(telemetryServiceOptions.ServiceName)
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
                                        httpContext.RequestUri.PathAndQuery.Contains("/loki") ||
                                        httpContext.RequestUri.PathAndQuery.Contains("swagger") ||
                                        httpContext.RequestUri.PathAndQuery.StartsWith("/health")
                                    ));
                            };
                        })
                        .AddSource("Yarp.ReverseProxy")
                        .AddConfluentKafkaInstrumentation());

                if (telemetryServiceOptions.InstrumentEntityFramework)
                {
                    otel.WithTracing(tracerProviderBuilder =>
                        tracerProviderBuilder
                            .AddEntityFrameworkCoreInstrumentation());
                }

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

                if (!string.IsNullOrEmpty(telemetryServiceOptions.MeterName))
                {
                    otel.WithMetrics(metricsProviderBuilder =>
                        metricsProviderBuilder
                            .AddMeter(telemetryServiceOptions.MeterName));
                }

                if (telemetryServiceOptions.EnableRuntimeInstrumentation)
                {
                    otel.WithMetrics(metricsProviderBuilder =>
                        metricsProviderBuilder
                            .AddRuntimeInstrumentation());
                }

                if (telemetryServiceOptions.Environment.IsDevelopment())
                {                  
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
            if (!telemetryServiceOptions.Environment.IsDevelopment() && telemetryServiceOptions.EnableAzureMonitor)
            {
                otel.UseAzureMonitor(options =>
                {
                    options.Credential = new DefaultAzureCredential();
                    options.ConnectionString = telemetryServiceOptions.AzureMonitorConnectionString;
                });
            }

            return services;
        }

        public class TelemetryServiceOptions : TelemetrySettings
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public string ServiceName { get; set; } = null!;
            public string? ServiceVersion { get; set; }            
        }
    }
}
