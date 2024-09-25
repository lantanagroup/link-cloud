package com.lantanagroup.link.measureeval.configs;
import com.lantanagroup.link.shared.config.AuthenticationConfig;
import com.lantanagroup.link.shared.auth.JwtAuthenticationEntryPoint;
import com.lantanagroup.link.shared.auth.JwtAuthenticationFilter;
import com.lantanagroup.link.shared.security.SecurityHelper;
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
      return SecurityHelper.build(http, point, authFilter);
    }
  }

  @ConditionalOnProperty(prefix = "authentication",
          name = "enableAnonymousAccess",
          havingValue = "false")
  @EnableMethodSecurity(prePostEnabled = true)
  static class Dummy {
  }
}
