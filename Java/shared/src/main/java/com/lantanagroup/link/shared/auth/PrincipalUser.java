package com.lantanagroup.link.shared.auth;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.Getter;
import lombok.Setter;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.userdetails.User;

import java.util.Collection;

@Getter
@Setter
@JsonInclude(JsonInclude.Include.NON_NULL)
public class PrincipalUser extends User {

  public PrincipalUser (String subject, Collection<? extends GrantedAuthority> authorities) {
    super(subject, "", true, true, true, true, authorities);
  }

}
