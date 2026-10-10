package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.shared.serdes.MultiDateDeserializer;
import com.lantanagroup.link.shared.services.ReportClient;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.MeasureReport;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Primary;
import org.springframework.http.converter.json.Jackson2ObjectMapperBuilder;
import org.springframework.web.client.RestClient;

import java.util.function.Predicate;

@Getter
@Setter
@Configuration
@ConfigurationProperties("link")
public class LinkConfig {
    private String reportabilityPredicate;
    private boolean cqlDebug = false;

    /**
     * Optional remote FHIR terminology service URL. When set, CQL evaluation federates
     * ValueSet / CodeSystem lookups: bundle-embedded resources are consulted first (as
     * before), then unresolved lookups fall through to this remote TS. Leaving this
     * unset preserves the existing in-memory-only behavior.
     */
    private String fhirTerminologyServiceUrl;

    /**
     * Remote FHIR terminology client. Consumed by {@code MeasureEvaluator} to build a
     * CQF {@code ProxyRepository} whose terminology tier federates the bundle with this
     * client (via {@code FederatedRepository + RestRepository}). Returns {@code null}
     * when {@link #fhirTerminologyServiceUrl} is not configured; callers must treat
     * null as "no remote terminology available" and use the plain in-memory repository
     * instead.
     */
    @Bean
    @Qualifier("remoteTerminologyClient")
    public IGenericClient remoteTerminologyClient(FhirContext fhirContext) {
        if (fhirTerminologyServiceUrl == null || fhirTerminologyServiceUrl.isBlank()) {
            return null;
        }
        return fhirContext.newRestfulGenericClient(fhirTerminologyServiceUrl);
    }

    @Bean
    @Primary
    public ObjectMapper objectMapper(Jackson2ObjectMapperBuilder builder) {
        ObjectMapper objectMapper = builder.createXmlMapper(false).build();
        objectMapper.registerModule(new JavaTimeModule());
        objectMapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        com.fasterxml.jackson.databind.module.SimpleModule module = new com.fasterxml.jackson.databind.module.SimpleModule();
        module.addDeserializer(java.util.Date.class, new MultiDateDeserializer());
        objectMapper.registerModule(module);
        return objectMapper;
    }

    @Bean
    @ConfigurationProperties("link.report")
    public ReportClient reportClient(JwtService jwtService, RestClient restClient) {
        return new ReportClient(jwtService, restClient);
    }

    @Bean
    @SuppressWarnings("unchecked")
    public Predicate<MeasureReport> reportabilityPredicate() throws Exception {
        return (Predicate<MeasureReport>) Class.forName(reportabilityPredicate).getConstructor().newInstance();
    }
}
