package com.lantanagroup.link.shared.auth;

import com.azure.security.keyvault.secrets.SecretClient;
import com.azure.security.keyvault.secrets.models.KeyVaultSecret;
import com.lantanagroup.link.shared.config.AuthenticationConfig;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;
import org.springframework.mock.web.MockFilterChain;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.servlet.HandlerExceptionResolver;

import java.nio.charset.StandardCharsets;
import java.util.Date;
import java.util.Optional;

public class JwtAuthenticationFilterTest {

  private static final String SIGNING_KEY = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
  private static final String OTHER_KEY = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

  private final HandlerExceptionResolver exceptionResolver = Mockito.mock(HandlerExceptionResolver.class);

  @AfterEach
  public void clearSecurityContext() {
    SecurityContextHolder.clearContext();
  }

  private AuthenticationConfig createConfig(String signingKey) {
    AuthenticationConfig config = new AuthenticationConfig();
    config.setAuthority("https://localhost:7004");
    config.setSigningKey(signingKey);
    return config;
  }

  private String createToken(JwtService jwtService, String secret) {
    return jwtService.generateToken(jwtService.getAdminUser(), secret);
  }

  @Test
  public void noVault_fallsBackToConfiguredSigningKey() throws Exception {
    AuthenticationConfig config = createConfig(SIGNING_KEY);
    JwtService jwtService = new JwtService(config);
    JwtAuthenticationFilter filter = new JwtAuthenticationFilter(config, jwtService, exceptionResolver, Optional.empty());

    MockHttpServletRequest request = new MockHttpServletRequest();
    request.addHeader("Authorization", "Bearer " + createToken(jwtService, SIGNING_KEY));
    MockFilterChain chain = new MockFilterChain();

    filter.doFilter(request, new MockHttpServletResponse(), chain);

    Assertions.assertNotNull(SecurityContextHolder.getContext().getAuthentication(),
        "Token signed with the configured signing key should authenticate when no Key Vault is available");
    Assertions.assertNotNull(chain.getRequest(), "Filter chain should proceed");
  }

  @Test
  public void noVault_tokenSignedWithOtherKeyIsRejected() throws Exception {
    AuthenticationConfig config = createConfig(SIGNING_KEY);
    JwtService jwtService = new JwtService(config);
    JwtAuthenticationFilter filter = new JwtAuthenticationFilter(config, jwtService, exceptionResolver, Optional.empty());

    MockHttpServletRequest request = new MockHttpServletRequest();
    request.addHeader("Authorization", "Bearer " + createToken(jwtService, OTHER_KEY));
    MockFilterChain chain = new MockFilterChain();

    filter.doFilter(request, new MockHttpServletResponse(), chain);

    Assertions.assertNull(SecurityContextHolder.getContext().getAuthentication(),
        "Token signed with a different key must not authenticate");
    Assertions.assertNotNull(chain.getRequest(), "Filter chain should proceed unauthenticated");
  }

  @Test
  public void noVault_expiredTokenIsRejected() throws Exception {
    AuthenticationConfig config = createConfig(SIGNING_KEY);
    JwtService jwtService = new JwtService(config);
    JwtAuthenticationFilter filter = new JwtAuthenticationFilter(config, jwtService, exceptionResolver, Optional.empty());

    String expired = Jwts.builder()
        .subject(JwtService.LinkUserClaims_LinkSystemAccount)
        .expiration(new Date(System.currentTimeMillis() - 60_000))
        .signWith(Keys.hmacShaKeyFor(SIGNING_KEY.getBytes(StandardCharsets.UTF_8)), Jwts.SIG.HS512)
        .compact();
    MockHttpServletRequest request = new MockHttpServletRequest();
    request.addHeader("Authorization", "Bearer " + expired);

    filter.doFilter(request, new MockHttpServletResponse(), new MockFilterChain());

    Assertions.assertNull(SecurityContextHolder.getContext().getAuthentication(),
        "An expired token must not authenticate");
  }

  @Test
  public void noVaultAndNoSigningKey_throwsSecurityException() {
    AuthenticationConfig config = createConfig(null);
    JwtService jwtService = new JwtService(config);
    JwtAuthenticationFilter filter = new JwtAuthenticationFilter(config, jwtService, exceptionResolver, Optional.empty());

    MockHttpServletRequest request = new MockHttpServletRequest();

    Assertions.assertThrows(SecurityException.class,
        () -> filter.doFilter(request, new MockHttpServletResponse(), new MockFilterChain()));
  }

  @Test
  public void vaultConfigured_vaultRemainsSourceOfTruth() throws Exception {
    // The config signing key is set to a DIFFERENT value; only a token signed with
    // the vault secret should validate, proving the fallback does not shadow the vault.
    AuthenticationConfig config = createConfig(OTHER_KEY);
    JwtService jwtService = new JwtService(config);

    SecretClient secretClient = Mockito.mock(SecretClient.class);
    Mockito.when(secretClient.getSecret(JwtService.Link_Bearer_Key))
        .thenReturn(new KeyVaultSecret(JwtService.Link_Bearer_Key, SIGNING_KEY));

    JwtAuthenticationFilter filter = new JwtAuthenticationFilter(config, jwtService, exceptionResolver, Optional.of(secretClient));

    MockHttpServletRequest request = new MockHttpServletRequest();
    request.addHeader("Authorization", "Bearer " + createToken(jwtService, SIGNING_KEY));

    filter.doFilter(request, new MockHttpServletResponse(), new MockFilterChain());

    Assertions.assertNotNull(SecurityContextHolder.getContext().getAuthentication(),
        "Token signed with the vault secret should authenticate when the vault is configured");
  }

  @Test
  public void vaultConfigured_tokenSignedWithConfigKeyIsRejected() throws Exception {
    AuthenticationConfig config = createConfig(OTHER_KEY);
    JwtService jwtService = new JwtService(config);

    SecretClient secretClient = Mockito.mock(SecretClient.class);
    Mockito.when(secretClient.getSecret(JwtService.Link_Bearer_Key))
        .thenReturn(new KeyVaultSecret(JwtService.Link_Bearer_Key, SIGNING_KEY));

    JwtAuthenticationFilter filter = new JwtAuthenticationFilter(config, jwtService, exceptionResolver, Optional.of(secretClient));

    MockHttpServletRequest request = new MockHttpServletRequest();
    request.addHeader("Authorization", "Bearer " + createToken(jwtService, OTHER_KEY));

    filter.doFilter(request, new MockHttpServletResponse(), new MockFilterChain());

    Assertions.assertNull(SecurityContextHolder.getContext().getAuthentication(),
        "Token signed with the local config key must NOT authenticate while a vault is configured");
  }

  @Test
  public void anonymousEnabled_skipsAuthentication() throws Exception {
    AuthenticationConfig config = createConfig(null);
    config.setAnonymous(true);
    JwtService jwtService = new JwtService(config);
    JwtAuthenticationFilter filter = new JwtAuthenticationFilter(config, jwtService, exceptionResolver, Optional.empty());

    MockFilterChain chain = new MockFilterChain();
    filter.doFilter(new MockHttpServletRequest(), new MockHttpServletResponse(), chain);

    Assertions.assertNotNull(chain.getRequest(), "Filter chain should proceed for anonymous access");
    Assertions.assertNull(SecurityContextHolder.getContext().getAuthentication());
  }
}
