package com.lantanagroup.link.shared.auth;

import com.lantanagroup.link.shared.config.AuthenticationConfig;
import io.jsonwebtoken.Claims;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.SignatureAlgorithm;

import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.function.Function;

import io.jsonwebtoken.security.Keys;
import org.apache.commons.lang3.StringUtils;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.stereotype.Component;

import javax.crypto.SecretKey;

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
  public static String Audiences = "LinkServices";
  public static String Email = "email";
  public static String Link_Bearer_Key = "link-bearer-key";

  private final AuthenticationConfig authenticationConfig;

  public JwtService(AuthenticationConfig authenticationConfig) {
    this.authenticationConfig = authenticationConfig;
  }

  //retrieve username from jwt token
  public String getUsernameFromToken (String token, String secret) {
    return getClaimFromToken(token, secret, Claims::getSubject);
  }

  public String getEmailFromToken (String token, String secret) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    return claims.get(JwtService.Email, String.class);
  }
  public List<String> getRolesFromToken (String token, String secret) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    Object roles = claims.get(JwtService.LinkSystemClaims_Role);
    if (roles instanceof List){
        return (List<String>)roles;
    }
    else{
      List<String> newList = new ArrayList<>();
      newList.add((String)roles);
      return newList;
    }
  }

  public List<String> getPermissionsFromToken (String token, String secret) {
    final Claims claims = getAllClaimsFromToken(token, secret);
    Object perm = claims.get(JwtService.LinkSystemClaims_LinkPermissions);
    if (perm instanceof List){
      return (List<String>)perm;
    }
    else{
      List<String> newList = new ArrayList<>();
      newList.add((String)perm);
      return newList;
    }
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
    SecretKey key = Keys.hmacShaKeyFor(secret.getBytes(StandardCharsets.UTF_8));
    return Jwts.parserBuilder().setSigningKey(key).build().parseClaimsJws(token).getBody();
  }

  //check if the token has expired
  private Boolean isTokenExpired (String token, String secret) {
    final Date expiration = getExpirationDateFromToken(token, secret);
    return expiration.before(new Date());
  }

  public PrincipalUser getAdminUser() {
    List<GrantedAuthority> authorities = List.of(
            new SimpleGrantedAuthority(RolePrefix + LinkUserClaims_LinkAdministrator),
            new SimpleGrantedAuthority(LinkSystemPermissions_IsLinkAdmin));
    return new PrincipalUser(LinkUserClaims_LinkSystemAccount, authenticationConfig.getAdminEmail(), authorities);
  }

  public String generateInterServiceToken() {
    if (authenticationConfig.isAnonymous()) {
      return null;
    }
    return generateToken(getAdminUser(), authenticationConfig.getSigningKey());
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

  private String doGenerateToken (Map<String, Object> claims, String subject, String secret) {
    if (this.authenticationConfig == null) {
      throw new RuntimeException("AuthenticationConfig is null");
    }
    if (StringUtils.isEmpty(this.authenticationConfig.getAuthority())) {
      throw new RuntimeException("authentication.authority is null");
    }
    if (StringUtils.isEmpty(secret)) {
      throw new RuntimeException("Signing key is empty");
    }

    SecretKey key = Keys.hmacShaKeyFor(secret.getBytes(StandardCharsets.UTF_8));
    return Jwts.builder().setHeaderParam("typ","JWT")
            .setClaims(claims)
            .setSubject(subject).
             setIssuedAt(new Date(System.currentTimeMillis()))
            .setIssuer(this.authenticationConfig.getAuthority())
            .setAudience(Audiences)
            .setExpiration(new Date(System.currentTimeMillis() + JWT_TOKEN_VALIDITY))
            .signWith(key, SignatureAlgorithm.HS512)
            .compact();
  }

  //validate token
  public Boolean validateToken (String token, String secret) {
    getUsernameFromToken(token, secret);
    return (!isTokenExpired(token, secret));
  }
}
