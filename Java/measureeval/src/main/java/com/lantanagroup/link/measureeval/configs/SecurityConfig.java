package com.lantanagroup.link.measureeval.configs;

import com.lantanagroup.link.shared.auth.JwtAuthenticationEntryPoint;
import com.lantanagroup.link.shared.auth.JwtAuthenticationFilter;
import com.lantanagroup.link.shared.config.AuthenticationConfig;
import com.lantanagroup.link.shared.security.SecurityHelper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.ComponentScan;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.annotation.Order;
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configuration.EnableWebSecurity;
import org.springframework.security.web.SecurityFilterChain;


@Configuration
@ComponentScan(basePackages = "com.lantanagroup.link.shared.auth")
@EnableWebSecurity
@Order(1)
public class SecurityConfig {
  private final JwtAuthenticationEntryPoint point;
  private final JwtAuthenticationFilter authFilter;
  private final AuthenticationConfig authenticationConfig;

  @Value("${link.info-route:/api/info}")
  private String infoRoute;

  public SecurityConfig(JwtAuthenticationEntryPoint point, JwtAuthenticationFilter authFilter, AuthenticationConfig authenticationConfig) {
    this.point = point;
    this.authFilter = authFilter;
    this.authenticationConfig = authenticationConfig;
  }

  @Bean
  public SecurityFilterChain securityFilterChain (HttpSecurity http) throws Exception {
    if (this.authenticationConfig.isAnonymous()) {
      return SecurityHelper.buildAnonymous(http);
    }
    else{
      return SecurityHelper.build(http, point, authFilter, infoRoute);
    }
  }

    /**
     * This class is necessary to support @PreAuthorize and @PostAuthorize when
     * anonymous mode is enabled. The condition causes the Dummy class to get registered
     * and for the @EnableMethodSecurity to be applied, which affects all classes, enabling @PreAuthorize/@PostAuthorize
     */
  @ConditionalOnProperty(prefix = "authentication",
          name = "anonymous",
          havingValue = "false")
  @EnableMethodSecurity(prePostEnabled = true)
  static class Dummy {
      private static final Logger logger = LoggerFactory.getLogger(Dummy.class);

      public Dummy() {
          logger.info("Anonymous authentication disabled - @EnableMethodSecurity is being applied (authentication.anonymous=false)");
      }
  }
}
