package com.lantanagroup.link.shared;

import com.lantanagroup.link.shared.auth.JwtAuthenticationEntryPoint;
import com.lantanagroup.link.shared.auth.JwtAuthenticationFilter;
import com.lantanagroup.link.shared.config.AuthenticationConfig;
import com.lantanagroup.link.shared.mongo.FhirConversions;
import com.lantanagroup.link.shared.security.SecurityHelper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.ComponentScan;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.web.SecurityFilterChain;

@Configuration
@ComponentScan(basePackages = "com.lantanagroup.link.shared.auth")
public class BaseSpringConfig {
    private static final Logger logger = LoggerFactory.getLogger(BaseSpringConfig.class);
    @Bean
    SecurityFilterChain web(
            AuthenticationConfig authenticationConfig,
            HttpSecurity http,
            JwtAuthenticationEntryPoint point,
            JwtAuthenticationFilter authFilter)
            throws Exception {
        if(authenticationConfig.isAnonymous()){
            logger.debug("Anonymous access is enabled");
        }

        return authenticationConfig.isAnonymous()
                ? SecurityHelper.buildAnonymous(http)
                : SecurityHelper.build(http, point, authFilter);
    }

    @Bean
    MongoCustomConversions customConversions() {
        return new FhirConversions();
    }
}
