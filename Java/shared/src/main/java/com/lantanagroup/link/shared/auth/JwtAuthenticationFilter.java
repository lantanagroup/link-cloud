package com.lantanagroup.link.shared.auth;

import com.azure.security.keyvault.secrets.SecretClient;
import org.apache.commons.lang3.StringUtils;
import com.lantanagroup.link.shared.config.AuthenticationConfig;
import io.jsonwebtoken.Claims;
import io.jsonwebtoken.security.SignatureException;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.web.authentication.WebAuthenticationDetailsSource;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;
import org.springframework.web.servlet.HandlerExceptionResolver;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

@Component
public class JwtAuthenticationFilter extends OncePerRequestFilter {
  private static final org.slf4j.Logger logger = org.slf4j.LoggerFactory.getLogger(JwtAuthenticationFilter.class);

  private final HandlerExceptionResolver handlerExceptionResolver;
  private final JwtService jwtService;
  private final AuthenticationConfig authenticationConfig;

  private SecretClient secretClient = null;

  public JwtAuthenticationFilter (AuthenticationConfig authenticationConfig, JwtService jwtService, HandlerExceptionResolver handlerExceptionResolver, Optional<SecretClient> secretClient) {
    super();

    this.authenticationConfig = authenticationConfig;
    this.secretClient = secretClient.orElse(null);
    this.jwtService = jwtService;
    this.handlerExceptionResolver = handlerExceptionResolver;
  }

  @Override
  protected void doFilterInternal (HttpServletRequest request, HttpServletResponse response, FilterChain filterChain) throws ServletException, IOException {

    String secret;

    // Allow anonymous access to the hosted REST API
    if (this.authenticationConfig.isAnonymous()) {
      logger.debug("Anonymous access is enabled");
      filterChain.doFilter(request, response);
      return;
    }

    if (this.secretClient == null) {
        throw new SecurityException("SecretClient is not configured");
    }

    secret = secretClient.getSecret(JwtService.Link_Bearer_Key).getValue();

    if (StringUtils.isBlank(secret)) {
      throw new SecurityException("JWT secret cannot be empty");
    }

    String authHeader = request.getHeader("Authorization");

    if (authHeader == null || !authHeader.startsWith("Bearer ")) {
     // logger.warn("JWT Token does not begin with Bearer String");
      filterChain.doFilter(request, response);
      return;
    }

    try {
      final String token = authHeader.substring(7);

      boolean validToken = false;
      try {
        validToken = jwtService.validateToken(token, secret);
      } catch (IllegalArgumentException e) {
        logger.warn("Illegal Argument while fetching the username." + e.getMessage());
      } catch (SignatureException e) {
        logger.warn("Given jwt token is not valid." + e.getMessage());
      } catch (Exception e) {
        logger.warn(e.getMessage());
      }

      if (validToken && SecurityContextHolder.getContext().getAuthentication() == null) {

        List<GrantedAuthority> authorities = new ArrayList<>();
        // get Claims from token
        Claims claims = jwtService.getAllClaimsFromToken(token, secret);

        // set roles in Granted Authority
        List<String> role = jwtService.getRolesFromToken(token, secret);
        GrantedAuthority grantedAuthority;
        for (String r : role) {
          grantedAuthority = new SimpleGrantedAuthority(JwtService.RolePrefix + r);
          authorities.add(grantedAuthority);
        }

        // set permissions in Granted Authority
        List<String> permissions = jwtService.getPermissionsFromToken(token, secret);
        for (String p : permissions) {
          grantedAuthority = new SimpleGrantedAuthority(p);
          authorities.add(grantedAuthority);
        }

        // set the authentication user with subject as username and authorities from token
        String email = jwtService.getEmailFromToken(token, secret);
        PrincipalUser user = new PrincipalUser(claims.getSubject(), email, authorities);

        UsernamePasswordAuthenticationToken authentication = new UsernamePasswordAuthenticationToken(user, null, user.getAuthorities());
        authentication.setDetails(new WebAuthenticationDetailsSource().buildDetails(request));
        SecurityContextHolder.getContext().setAuthentication(authentication);
      } else {
        logger.warn("Token Validation failed !!");
      }
    } catch (Exception e) {
        handlerExceptionResolver.resolveException(request, response, null, e);
    }

    filterChain.doFilter(request, response);
  }
}
