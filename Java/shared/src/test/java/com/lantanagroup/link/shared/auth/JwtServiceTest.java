package com.lantanagroup.link.shared.auth;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.config.AuthenticationConfig;
import io.jsonwebtoken.ExpiredJwtException;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import io.jsonwebtoken.security.SignatureException;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.Date;
import java.util.List;
import java.util.Map;

public class JwtServiceTest {

  private static final String SIGNING_KEY = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
  private static final String OTHER_KEY = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
  private static final String AUTHORITY = "https://localhost:7004";

  private JwtService createService(boolean anonymous) {
    AuthenticationConfig config = new AuthenticationConfig();
    config.setAuthority(AUTHORITY);
    config.setSigningKey(SIGNING_KEY);
    config.setAnonymous(anonymous);
    return new JwtService(config);
  }

  private JsonNode decodePart(String token, int index) throws Exception {
    String part = token.split("\\.")[index];
    return new ObjectMapper().readTree(Base64.getUrlDecoder().decode(part));
  }

  private String signedToken(Map<String, Object> claims, Date expiration, String key) {
    return Jwts.builder()
            .claims(claims)
            .subject("SystemAccount")
            .issuer(AUTHORITY)
            .audience()
            .single(JwtService.Audiences)
            .expiration(expiration)
            .signWith(Keys.hmacShaKeyFor(key.getBytes(StandardCharsets.UTF_8)), Jwts.SIG.HS512)
            .compact();
  }

  @Test
  public void generateToken_keepsTheShapeDotNetValidates() throws Exception {
    JwtService jwtService = createService(false);

    String token = jwtService.generateToken(jwtService.getAdminUser(), SIGNING_KEY);

    JsonNode header = decodePart(token, 0);
    Assertions.assertEquals("HS512", header.get("alg").asText());
    Assertions.assertEquals("JWT", header.get("typ").asText());

    JsonNode payload = decodePart(token, 1);
    Assertions.assertTrue(payload.get("aud").isTextual(), "aud must stay a single string, not an array");
    Assertions.assertEquals(JwtService.Audiences, payload.get("aud").asText());
    Assertions.assertEquals(AUTHORITY, payload.get("iss").asText());
    Assertions.assertEquals(JwtService.LinkUserClaims_LinkSystemAccount, payload.get("sub").asText());
    Assertions.assertEquals(JwtService.LinkUserClaims_LinkAdministrator, payload.get("roles").asText());
    Assertions.assertEquals(JwtService.LinkSystemPermissions_IsLinkAdmin, payload.get("permissions").asText());
    Assertions.assertTrue(payload.get("exp").asLong() > payload.get("iat").asLong());
  }

  @Test
  public void validateToken_ownToken_isValid() {
    JwtService jwtService = createService(false);

    String token = jwtService.generateToken(jwtService.getAdminUser(), SIGNING_KEY);

    Assertions.assertTrue(jwtService.validateToken(token, SIGNING_KEY));
    Assertions.assertEquals(List.of(JwtService.LinkUserClaims_LinkAdministrator),
        jwtService.getRolesFromToken(token, SIGNING_KEY));
    Assertions.assertEquals(List.of(JwtService.LinkSystemPermissions_IsLinkAdmin),
        jwtService.getPermissionsFromToken(token, SIGNING_KEY));
  }

  @Test
  public void validateToken_otherKey_throwsSignatureException() {
    JwtService jwtService = createService(false);

    String token = jwtService.generateToken(jwtService.getAdminUser(), OTHER_KEY);

    Assertions.assertThrows(SignatureException.class, () -> jwtService.validateToken(token, SIGNING_KEY));
  }

  @Test
  public void validateToken_expiredToken_throwsExpiredJwtException() {
    JwtService jwtService = createService(false);

    String token = signedToken(Map.of(), new Date(System.currentTimeMillis() - 60_000), SIGNING_KEY);

    Assertions.assertThrows(ExpiredJwtException.class, () -> jwtService.validateToken(token, SIGNING_KEY));
  }

  @Test
  public void getRolesFromToken_rolesArray_returnsEveryRole() {
    // .NET writes a multi-role claim as an array; the Java side wrote a single string.
    JwtService jwtService = createService(false);
    Map<String, Object> claims = Map.of(
        JwtService.LinkSystemClaims_Role, List.of("LinkAdministrator", "LinkUser"),
        JwtService.Email, "admin@example.invalid");

    String token = signedToken(claims, new Date(System.currentTimeMillis() + 60_000), SIGNING_KEY);

    Assertions.assertEquals(List.of("LinkAdministrator", "LinkUser"), jwtService.getRolesFromToken(token, SIGNING_KEY));
    Assertions.assertEquals("admin@example.invalid", jwtService.getEmailFromToken(token, SIGNING_KEY));
  }

  @Test
  public void generateInterServiceToken_authenticated_returnsTokenSignedWithConfiguredKey() {
    JwtService jwtService = createService(false);

    String token = jwtService.generateInterServiceToken();

    Assertions.assertNotNull(token);
    Assertions.assertTrue(jwtService.validateToken(token, SIGNING_KEY));
  }

  @Test
  public void generateInterServiceToken_anonymous_returnsNull() {
    JwtService jwtService = createService(true);

    Assertions.assertNull(jwtService.generateInterServiceToken());
  }
}
