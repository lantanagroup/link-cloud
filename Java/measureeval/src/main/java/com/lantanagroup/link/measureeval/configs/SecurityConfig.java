package com.lantanagroup.link.measureeval.configs;
import com.lantanagroup.link.shared.auth.JwtAuthenticationEntryPoint;
import com.lantanagroup.link.shared.auth.JwtAuthenticationFilter;
import com.lantanagroup.link.shared.security.SecurityHelper;
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
import org.springframework.beans.factory.annotation.Autowired;


@Configuration
@ComponentScan(basePackages = "com.lantanagroup.link.shared.auth")
@EnableWebSecurity
@Order(1)
public class SecurityConfig {

  @Autowired
  private JwtAuthenticationEntryPoint point;

  @Autowired
  private JwtAuthenticationFilter authFilter;


  @Value("${authentication.enableAnonymousAccess}")
  private boolean anonymousAccessEnabled;


  @Bean
  public SecurityFilterChain securityFilterChain (HttpSecurity http) throws Exception {
    if (anonymousAccessEnabled) {
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
