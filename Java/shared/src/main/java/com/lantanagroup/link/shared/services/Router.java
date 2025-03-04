package com.lantanagroup.link.shared.services;

import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.springframework.web.util.UriComponentsBuilder;

import java.net.URI;
import java.util.List;
import java.util.Map;

@Getter
@Setter
public class Router {
    private String baseUrl;
    private Map<String, Route> routes = Map.of();

    public URI getUri(String key, Map<String, ?> variables) {
        if (StringUtils.isEmpty(baseUrl)) {
            throw new IllegalStateException("Base URL is empty");
        }
        Route route = routes.get(key);
        if (route == null) {
            throw new IllegalStateException("Route not found");
        }
        UriComponentsBuilder builder = UriComponentsBuilder.fromHttpUrl(baseUrl);
        builder.path(route.path);
        for (String query : route.query) {
            builder.query(query);
        }
        return builder.build(variables);
    }

    @Getter
    @Setter
    public static class Route {
        private String path;
        private List<String> query = List.of();
    }
}
