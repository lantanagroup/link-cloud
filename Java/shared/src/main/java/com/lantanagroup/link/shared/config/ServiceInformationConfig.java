package com.lantanagroup.link.shared.config;

import com.fasterxml.jackson.annotation.JsonIgnore;
import jakarta.annotation.PostConstruct;
import lombok.Getter;
import lombok.Setter;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.info.BuildProperties;

@ConfigurationProperties("service-information")
@Getter
@Setter
public class ServiceInformationConfig {
    private String serviceName;
    private String version;
    private String productVersion;
    private String build;

    @Autowired(required = false)
    @JsonIgnore
    private BuildProperties buildProperties;

    @PostConstruct
    public void initializeVersion() {
        if (this.version == null || this.version.isEmpty()) {
            this.version = buildProperties != null ? buildProperties.getVersion() : "UNKNOWN";
        }
    }
}
