package com.lantanagroup.link.measureeval.config;

import com.lantanagroup.link.measureeval.services.DataAcquisitionClient;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.client.RestClient;

@Configuration
public class Config {
    @Bean
    @ConfigurationProperties("link.data-acquisition")
    public DataAcquisitionClient dataAcquisitionClient(RestClient restClient) {
        return new DataAcquisitionClient(restClient);
    }
}
