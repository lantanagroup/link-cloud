package com.lantanagroup.link.measureeval.configs;

import com.lantanagroup.link.shared.config.TelemetryConfig;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.trace.propagation.W3CTraceContextPropagator;
import io.opentelemetry.context.propagation.ContextPropagators;
import io.opentelemetry.context.propagation.TextMapPropagator;
import io.opentelemetry.exporter.otlp.metrics.OtlpGrpcMetricExporter;
import io.opentelemetry.exporter.otlp.trace.OtlpGrpcSpanExporter;
import io.opentelemetry.instrumentation.runtimemetrics.java17.RuntimeMetrics;
import io.opentelemetry.sdk.OpenTelemetrySdk;
import io.opentelemetry.sdk.metrics.SdkMeterProvider;
import io.opentelemetry.sdk.metrics.export.PeriodicMetricReader;
import io.opentelemetry.sdk.resources.Resource;
import io.opentelemetry.sdk.trace.SdkTracerProvider;
import io.opentelemetry.sdk.trace.export.BatchSpanProcessor;
import io.opentelemetry.semconv.ResourceAttributes;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.util.UUID;


@Configuration
public class OpenTelemetryConfig {
  static final String SERVICE_INSTANCE_ID = "service.instance.id";

  private final Logger logger = LoggerFactory.getLogger(OpenTelemetryConfig.class);
  private final TelemetryConfig telemetryConfig;

  @Value("${spring.application.name}")
  private String serviceName;

  public OpenTelemetryConfig (TelemetryConfig telemetryConfig) {
    this.telemetryConfig = telemetryConfig;
  }

  @Bean
  public OpenTelemetry openTelemetry () {
    if (this.telemetryConfig == null || this.telemetryConfig.getExporterEndpoint() == null) {
      logger.warn("Telemetry configuration is not set. OpenTelemetry will not be initialized.");
      return OpenTelemetry.noop();
    }

    Resource resource = buildResource(serviceName, this.telemetryConfig.getDeploymentEnvironment());

    SdkTracerProvider sdkTracerProvider = SdkTracerProvider.builder()
            .addSpanProcessor(BatchSpanProcessor.builder(OtlpGrpcSpanExporter.builder().setEndpoint(this.telemetryConfig.getExporterEndpoint()).build()).build())
            .setResource(resource)
            .build();

    SdkMeterProvider sdkMeterProvider = SdkMeterProvider.builder()
            .registerMetricReader(PeriodicMetricReader.builder(OtlpGrpcMetricExporter.builder().setEndpoint(this.telemetryConfig.getExporterEndpoint()).build()).build())
            .setResource(resource)
            .build();

    /*
    SdkLoggerProvider sdkLoggerProvider = SdkLoggerProvider.builder()
            .addLogRecordProcessor(BatchLogRecordProcessor.builder(OtlpGrpcLogRecordExporter.builder().setEndpoint(this.telemetryConfig.getExporterEndpoint()).build()).build())
            .setResource(resource)
            .build();
     */

    OpenTelemetrySdk openTelemetrySdk = OpenTelemetrySdk.builder()
            .setTracerProvider(sdkTracerProvider)
            .setMeterProvider(sdkMeterProvider)
            //.setLoggerProvider(sdkLoggerProvider)
            .setPropagators(ContextPropagators.create(TextMapPropagator.composite(W3CTraceContextPropagator.getInstance())))
            .buildAndRegisterGlobal();

    RuntimeMetrics.builder(openTelemetrySdk).enableAllFeatures().build();

    Runtime.getRuntime().addShutdownHook(new Thread(sdkTracerProvider::close));

    return openTelemetrySdk;
  }

  /**
   * Names the service, the process and, when one is configured, the environment. Without an instance
   * id, every replica and every environment sharing a Prometheus write to the same series and
   * overwrite each other's counters; .NET generates one by default.
   */
  static Resource buildResource(String serviceName, String deploymentEnvironment) {
    var builder = Resource.getDefault().toBuilder()
            .put(ResourceAttributes.SERVICE_NAME, serviceName)
            .put(SERVICE_INSTANCE_ID, UUID.randomUUID().toString());

    if (deploymentEnvironment == null || deploymentEnvironment.isBlank()) {
      return builder.build();
    }

    return builder
            .put(DiagnosticNames.DEPLOYMENT_ENVIRONMENT_NAME, deploymentEnvironment)
            .build();
  }
}
