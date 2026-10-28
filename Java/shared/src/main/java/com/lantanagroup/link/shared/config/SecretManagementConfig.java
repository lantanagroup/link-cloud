package com.lantanagroup.link.shared.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("secret-management")
@Getter @Setter
public class SecretManagementConfig {
    private String keyVaultUri;
}
