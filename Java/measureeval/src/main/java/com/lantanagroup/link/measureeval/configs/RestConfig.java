package com.lantanagroup.link.measureeval.configs;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.converter.json.MappingJackson2HttpMessageConverter;
import org.springframework.web.client.RestClient;

@Configuration
public class RestConfig {
    @Bean
    public RestClient restClient(ObjectMapper objectMapper) {
        return RestClient.builder()
                .messageConverters(converters -> converters.stream()
                        .filter(MappingJackson2HttpMessageConverter.class::isInstance)
                        .map(MappingJackson2HttpMessageConverter.class::cast)
                        .forEach(converter -> converter.setObjectMapper(objectMapper)))
                .build();
    }
}
