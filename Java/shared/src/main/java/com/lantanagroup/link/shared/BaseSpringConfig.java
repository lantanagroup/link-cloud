package com.lantanagroup.link.shared;

import com.lantanagroup.link.shared.auth.JwtAuthenticationEntryPoint;
import com.lantanagroup.link.shared.auth.JwtAuthenticationFilter;
import com.lantanagroup.link.shared.config.AuthenticationConfig;
import com.lantanagroup.link.shared.mongo.FhirConversions;
import com.lantanagroup.link.shared.security.SecurityHelper;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.ComponentScan;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.web.SecurityFilterChain;

@Configuration
@ComponentScan(basePackages = "com.lantanagroup.link.shared.auth")
public class BaseSpringConfig {
    @Bean
    SecurityFilterChain web(
            AuthenticationConfig authenticationConfig,
            HttpSecurity http,
            JwtAuthenticationEntryPoint point,
            JwtAuthenticationFilter authFilter)
            throws Exception {
        return authenticationConfig.isAnonymous()
                ? SecurityHelper.buildAnonymous(http)
                : SecurityHelper.build(http, point, authFilter);
    }

    @Bean
    MongoCustomConversions customConversions() {
        return new FhirConversions();
    }
}
