package com.lantanagroup.link.shared.audit;

import io.opentelemetry.api.OpenTelemetry;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.autoconfigure.AutoConfiguration;
import org.springframework.boot.autoconfigure.condition.ConditionalOnClass;
import org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean;
import org.springframework.boot.autoconfigure.condition.ConditionalOnWebApplication;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;
import org.springframework.web.servlet.mvc.method.annotation.RequestMappingHandlerMapping;

import java.time.Clock;
import java.time.Duration;

/**
 * Registers the shared audit mechanism in every servlet service that has Spring Kafka. A service with no
 * {@link Auditable} method creates no producer. Nothing Kafka-related is resolved at startup: the service's
 * {@link KafkaProperties} and {@link SslBundles} are looked up on the first publish, so a controller test slice
 * with no Kafka configuration still starts.
 * <p>
 * A service that has Hibernate also gets {@link AuditJpaConfiguration}, which captures property changes from
 * its saves.
 */
@AutoConfiguration
@ConditionalOnWebApplication(type = ConditionalOnWebApplication.Type.SERVLET)
@ConditionalOnClass(KafkaTemplate.class)
@EnableConfigurationProperties(AuditProperties.class)
@Import(AuditJpaConfiguration.class)
public class AuditAutoConfiguration {

    @Bean
    AuditMasking auditMasking(ObjectProvider<AuditMaskingCustomizer> customizers) {
        return new AuditMasking(customizers.orderedStream().toList());
    }

    @Bean
    AuditDiff auditDiff(AuditMasking auditMasking) {
        return new AuditDiff(auditMasking);
    }

    @Bean
    @ConditionalOnMissingBean
    AuditContext auditContext(AuditDiff auditDiff) {
        return new RequestAuditContext(auditDiff);
    }

    @Bean
    AuditEventFactory auditEventFactory(AuditProperties properties) {
        return new AuditEventFactory(properties, Clock.systemUTC());
    }

    @Bean
    AuditMetrics auditMetrics(ObjectProvider<OpenTelemetry> openTelemetry, AuditProperties properties) {
        // getIfUnique: two OpenTelemetry beans fall back to the no-op meter and its one warning, not a failed startup.
        return new AuditMetrics(openTelemetry.getIfUnique(), properties.getServiceName());
    }

    @Bean
    @ConditionalOnMissingBean
    AuditPublisher auditPublisher(ObjectProvider<KafkaProperties> kafkaProperties,
                                  ObjectProvider<SslBundles> sslBundles,
                                  AuditProperties properties, AuditMetrics auditMetrics) {
        Duration timeout = properties.getPublish().getResponseTimeout();
        return new KafkaAuditPublisher(
                () -> KafkaAuditPublisher.producerFactory(KafkaAuditPublisher.producerConfig(
                        kafkaProperties.getObject().buildProducerProperties(sslBundles.getIfAvailable()), timeout)),
                timeout, auditMetrics);
    }

    @Bean
    AuditInterceptor auditInterceptor(AuditEventFactory auditEventFactory, AuditPublisher auditPublisher,
                                      AuditMetrics auditMetrics) {
        return new AuditInterceptor(auditEventFactory, auditPublisher, auditMetrics);
    }

    @Bean
    WebMvcConfigurer auditWebMvcConfigurer(AuditInterceptor auditInterceptor) {
        return new WebMvcConfigurer() {
            @Override
            public void addInterceptors(InterceptorRegistry registry) {
                registry.addInterceptor(auditInterceptor);
            }
        };
    }

    @Bean
    AuditStartupCheck auditStartupCheck(ObjectProvider<RequestMappingHandlerMapping> mappings,
                                        AuditProperties properties) {
        return new AuditStartupCheck(mappings, properties);
    }
}
