package com.lantanagroup.link.measureeval.services;

import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.springframework.web.util.UriComponentsBuilder;

import java.net.URI;
import java.util.Map;

@Getter
@Setter
public class Router {
    private String baseUrl;
    private Map<String, String> routes = Map.of();

    public URI getUri(String key, Map<String, ?> variables) {
        if (StringUtils.isEmpty(baseUrl)) {
            throw new IllegalStateException("Base URL is empty");
        }
        String route = routes.get(key);
        if (route == null) {
            throw new IllegalStateException("Route not found");
        }
        return UriComponentsBuilder.fromHttpUrl(baseUrl)
                .path(route)
                .build(variables);
    }
}
