package com.lantanagroup.link.measureeval.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.util.UriComponentsBuilder;

import java.util.Map;

@Getter
@Setter
@Configuration
@ConfigurationProperties("link.data-acquisition")
public class DataAcquisitionConfig {
    private String baseUrl;
    private Map<String, String> routes = Map.of(
            "query-result", "/api/{facilityId}/QueryResult/{patientId}");

    private String getUrl(String key, Map<String, ?> variables) {
        return UriComponentsBuilder.fromHttpUrl(baseUrl)
                .path(routes.get(key))
                .build(variables)
                .toString();
    }

    public String getQueryResultUrl(String facilityId, String patientId) {
        return getUrl("query-result", Map.of(
                "facilityId", facilityId,
                "patientId", patientId));
    }
}
