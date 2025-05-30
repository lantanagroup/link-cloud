package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.shared.config.TelemetryConfig;
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


@Configuration
public class OpenTelemetryConfig {
    private final Logger logger = LoggerFactory.getLogger(OpenTelemetryConfig.class);
    private final TelemetryConfig telemetryConfig;

    @Value("${spring.application.name}")
    private String serviceName;

    public OpenTelemetryConfig (TelemetryConfig telemetryConfig) {
        this.telemetryConfig = telemetryConfig;
    }

    @Bean
    public OpenTelemetry openTelemetry () {
        Resource resource = Resource.getDefault().toBuilder().put(ResourceAttributes.SERVICE_NAME, serviceName).build();

        if (this.telemetryConfig == null || this.telemetryConfig.getExporterEndpoint() == null) {
            logger.warn("Telemetry configuration is not set. OpenTelemetry will not be initialized.");
            return OpenTelemetry.noop();
        }

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
}
