package com.lantanagroup.link.measureeval.models;

import lombok.Getter;
import lombok.Setter;

import java.util.HashMap;
import java.util.Map;

@Getter
@Setter
public class RelatedArtifactInfo {
    private String name;
    private String url;
    private boolean found;
    private Map<String, String> libraries = new HashMap<>();
    private String version;
}
