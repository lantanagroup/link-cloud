package com.lantanagroup.link.measureeval.configs;
import com.lantanagroup.link.measureeval.auth.*;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpMethod;
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configuration.EnableWebSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.web.authentication.UsernamePasswordAuthenticationFilter;


@Configuration
@EnableWebSecurity
@EnableMethodSecurity
@Order(1)
public class SecurityConfig {

  @Autowired
  private JwtAuthenticationEntryPoint point;

  @Autowired
  private JwtAuthenticationFilter authFilter;

  @Bean
  public SecurityFilterChain securityFilterChain (HttpSecurity http) throws Exception {

    http.csrf(AbstractHttpConfigurer::disable).addFilterBefore(authFilter, UsernamePasswordAuthenticationFilter.class)
    .authorizeRequests().
    requestMatchers(HttpMethod.OPTIONS, "/**").permitAll()
    .requestMatchers(HttpMethod.GET, "/health").permitAll()
    .requestMatchers(HttpMethod.GET, "/api-docs/**").permitAll()
    .requestMatchers(HttpMethod.GET, "/swagger-ui/**").permitAll()
    //requestMatchers("/api/**").access("hasRole('LinkUser') and hasAuthority('IsLinkAdmin1')").and().exceptionHandling(ex -> ex.authenticationEntryPoint(point)  - done in the specific end points using annotations for more granular control
    .requestMatchers("/api/**").authenticated().and().exceptionHandling(ex -> ex.authenticationEntryPoint(point))
    .sessionManagement(session -> session.sessionCreationPolicy(SessionCreationPolicy.STATELESS));
    return http.build();
  }

}
