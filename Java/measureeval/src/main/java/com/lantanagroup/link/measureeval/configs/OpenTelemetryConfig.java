package com.lantanagroup.link.measureeval.configs;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.context.propagation.TextMapPropagator;
import io.opentelemetry.exporter.otlp.logs.OtlpGrpcLogRecordExporter;
import io.opentelemetry.exporter.otlp.metrics.OtlpGrpcMetricExporter;
import io.opentelemetry.exporter.otlp.trace.OtlpGrpcSpanExporter;
import io.opentelemetry.sdk.trace.export.BatchSpanProcessor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import io.opentelemetry.api.trace.propagation.W3CTraceContextPropagator;
import io.opentelemetry.context.propagation.ContextPropagators;
import io.opentelemetry.sdk.OpenTelemetrySdk;
import io.opentelemetry.sdk.metrics.SdkMeterProvider;
import io.opentelemetry.sdk.metrics.export.PeriodicMetricReader;
import io.opentelemetry.sdk.resources.Resource;
import io.opentelemetry.sdk.trace.SdkTracerProvider;
import io.opentelemetry.sdk.logs.SdkLoggerProvider;
import io.opentelemetry.sdk.logs.export.BatchLogRecordProcessor;
import io.opentelemetry.semconv.ResourceAttributes;

@Configuration
public class OpenTelemetryConfig {

  @Value("${telemetryConfig.exporter.endpoint}")
  private String exporterEndpoint;

  @Value("${spring.application.name}")
  private String serviceName;

@Bean
  public OpenTelemetry openTelemetry() {
    Resource resource = Resource.getDefault().toBuilder().put(ResourceAttributes.SERVICE_NAME, serviceName).build();


    SdkTracerProvider sdkTracerProvider = SdkTracerProvider.builder()
            .addSpanProcessor(BatchSpanProcessor.builder(OtlpGrpcSpanExporter.builder().setEndpoint(exporterEndpoint).build()).build())
            .setResource(resource)
            .build();

    SdkMeterProvider sdkMeterProvider = SdkMeterProvider.builder()
            .registerMetricReader(PeriodicMetricReader.builder(OtlpGrpcMetricExporter.builder().setEndpoint(exporterEndpoint).build()).build())
            .setResource(resource)
            .build();

    SdkLoggerProvider sdkLoggerProvider = SdkLoggerProvider.builder()
            .addLogRecordProcessor(BatchLogRecordProcessor.builder(OtlpGrpcLogRecordExporter.builder().setEndpoint(exporterEndpoint).build()).build())
            .setResource(resource)
            .build();

  OpenTelemetrySdk sdk = OpenTelemetrySdk.builder()
            .setTracerProvider(sdkTracerProvider)
            .setMeterProvider(sdkMeterProvider)
          //  .setLoggerProvider(sdkLoggerProvider)
            .setPropagators(ContextPropagators.create(TextMapPropagator.composite(W3CTraceContextPropagator.getInstance())))
            .buildAndRegisterGlobal();

    Runtime.getRuntime().addShutdownHook(new Thread(sdkTracerProvider::close));
    return sdk;
  }
}
