package com.lantanagroup.link.shared.auth;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.SignatureAlgorithm;

import java.nio.charset.StandardCharsets;
import java.util.Date;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.function.Function;

import org.springframework.stereotype.Component;

@Component
public class JwtService {
  public final long JWT_TOKEN_VALIDITY = 5 * 60 * 1000;
  public static String LinkSystemClaims_Role = "roles";
  public static String LinkSystemClaims_LinkPermissions = "permissions";
  public static String LinkSystemClaims_Subject = "sub";
  public static String LinkUserClaims_LinkAdministrator = "LinkAdministrator";
  public static String LinkSystemPermissions_IsLinkAdmin = "IsLinkAdmin";
  public static String LinkUserClaims_LinkSystemAccount = "SystemAccount";
  public static String RolePrefix = "ROLE_";
  public static String Authority = "https://dev-bff.nhsnlink.org";

  public static String Link_Bearer_Key = "link-bearer-key";

  //retrieve username from jwt token
  public String getUsernameFromToken (String token, String secret) {
    return getClaimFromToken(token, secret, Claims::getSubject);
  }

  public List<String> getRolesFromToken (String token, String secret) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    return (List<String>)claims.get(JwtService.LinkSystemClaims_Role);
  }

  public List<String> getPermissionsFromToken (String token, String secret) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    return (List<String>)claims.get(JwtService.LinkSystemClaims_LinkPermissions);
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
  public String generateToken (PrincipalUser user, String secret) {
    Map<String, Object> claims = new HashMap<>();
    // get claims from user.getAuthorities
    user.getAuthorities().forEach(authority -> {
      if(authority.getAuthority().contains(JwtService.RolePrefix)) {
        claims.put(JwtService.LinkSystemClaims_Role, authority.getAuthority().substring(JwtService.RolePrefix.length()));
      }
      else {
        claims.put(JwtService.LinkSystemClaims_LinkPermissions, authority.getAuthority());
      }
    });
    claims.put(JwtService.LinkSystemClaims_Subject, user.getUsername());
    return doGenerateToken(claims, user.getUsername(), secret);
  }

  //while creating the token -
  //1. Define  claims of the token, like Issuer, Expiration, Subject, and the ID
  //2. Sign the JWT using the HS512 algorithm and secret key.
  //3. According to JWS Compact Serialization(https://tools.ietf.org/html/draft-ietf-jose-json-web-signature-41#section-3.1)
  //   compaction of the JWT to a URL-safe string
  private String doGenerateToken (Map<String, Object> claims, String subject, String secret) {
    return Jwts.builder().setHeaderParam("typ","JWT")
            .setClaims(claims)
            .setSubject(subject).
             setIssuedAt(new Date(System.currentTimeMillis()))
            .setIssuer(Authority)
            .setExpiration(new Date(System.currentTimeMillis() + JWT_TOKEN_VALIDITY))
            .signWith(SignatureAlgorithm.HS512, secret.getBytes(StandardCharsets.UTF_8)).compact();
  }

  //validate token
  public Boolean validateToken (String token, String secret) {
    getUsernameFromToken(token, secret);
    return (!isTokenExpired(token, secret));
  }
}
