package com.lantanagroup.link.shared.config;

import com.fasterxml.jackson.annotation.JsonIgnore;
import jakarta.annotation.PostConstruct;
import lombok.Getter;
import lombok.Setter;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
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
    private String commit;
    private String swaggerUrl;

    @Value("${springdoc.swagger-ui.enabled:false}")
    private boolean swaggerUiEnabled;

    @Autowired(required = false)
    @JsonIgnore
    private BuildProperties buildProperties;

    @PostConstruct
    public void initialize() {
        if (this.version == null || this.version.isEmpty()) {
            this.version = buildProperties != null ? buildProperties.getVersion() : "UNKNOWN";
        }

        if (this.swaggerUiEnabled) {
            this.swaggerUrl = "/swagger-ui.html";
        }
    }
}
