package com.lantanagroup.link.shared.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("authentication")
@Getter @Setter
public class AuthenticationConfig {
    private String authority;
    private boolean isAnonymous = false;
    private String adminEmail = "";
    private String signingKey;
}
