package com.lantanagroup.link.shared.security;

import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.web.SecurityFilterChain;

public class SecurityHelper {
    public static SecurityFilterChain build(HttpSecurity http) throws Exception {
        // TODO: This needs to be updated to secure the application
        http
                .authorizeHttpRequests((authorizeRequests) ->
                        authorizeRequests
                                .anyRequest()
                                .anonymous());
        return http.build();
    }
}
