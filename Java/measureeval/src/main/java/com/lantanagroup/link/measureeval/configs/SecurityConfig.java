package com.lantanagroup.link.measureeval.configs;

import com.lantanagroup.link.shared.security.SecurityHelper;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.web.SecurityFilterChain;

@Configuration
public class SecurityConfig {
    @Bean
    public SecurityFilterChain securityFilterChain(HttpSecurity http) throws Exception {
        return SecurityHelper.buildAnonymous(http);
    }
}
