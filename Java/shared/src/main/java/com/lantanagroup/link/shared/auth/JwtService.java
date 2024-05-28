package com.lantanagroup.link.shared.auth;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.SignatureAlgorithm;

import java.nio.charset.StandardCharsets;
import java.util.Date;
import java.util.HashMap;
import java.util.Map;
import java.util.function.Function;

import org.springframework.stereotype.Component;

@Component
public class JwtService {
  public final long JWT_TOKEN_VALIDITY = 5 * 60 * 60;

  //retrieve username from jwt token
  public String getUsernameFromToken (String token, String secret) {
    return getClaimFromToken(token, secret, Claims::getSubject);
  }

  public String getRolesFromToken (String token, String secret) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    return claims.get("roles", String.class);
  }

  public String getPermissionsFromToken (String token, String secret) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    return claims.get("permissions", String.class);
  }

  //retrieve expiration date from jwt token
  public Date getExpirationDateFromToken (String token, String secret) {
    return getClaimFromToken(token, secret, Claims::getExpiration);
  }

  public <T> T getClaimFromToken (String token, String secret, Function<Claims, T> claimsResolver) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    return claimsResolver.apply(claims);
  }

  //for retrieving any information from token we will need the secret key
  public Claims getAllClaimsFromToken (String token, String secret) {
    return Jwts.parser().setSigningKey(secret.getBytes(StandardCharsets.UTF_8)).parseClaimsJws(token).getBody();
  }

  //check if the token has expired
  private Boolean isTokenExpired (String token, String secret) {
    final Date expiration = getExpirationDateFromToken(token, secret);
    return expiration.before(new Date());
  }

  //generate token for user
  public String generateToken (PrincipalUser userDetails, String secret) {
    Map<String, Object> claims = new HashMap<>();
    return doGenerateToken(claims, userDetails.getUsername(), secret);
  }

  //while creating the token -
  //1. Define  claims of the token, like Issuer, Expiration, Subject, and the ID
  //2. Sign the JWT using the HS512 algorithm and secret key.
  //3. According to JWS Compact Serialization(https://tools.ietf.org/html/draft-ietf-jose-json-web-signature-41#section-3.1)
  //   compaction of the JWT to a URL-safe string
  private String doGenerateToken (Map<String, Object> claims, String subject, String secret) {
    return Jwts.builder().setClaims(claims).setSubject(subject).setIssuedAt(new Date(System.currentTimeMillis()))
            .setExpiration(new Date(System.currentTimeMillis() + JWT_TOKEN_VALIDITY * 1000))
            .signWith(SignatureAlgorithm.HS512, secret.getBytes(StandardCharsets.UTF_8)).compact();
  }

  //validate token
  public Boolean validateToken (String token, String secret) {
    getUsernameFromToken(token, secret);
    return (!isTokenExpired(token, secret));
  }
}
