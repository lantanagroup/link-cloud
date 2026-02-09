package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.ArrayList;
import java.util.List;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class TerminologyDependency {
    private String url;
    private String version;
    private boolean resourceExists;
    private boolean versionExists;
    private List<SourceProfile> sourceProfile = new ArrayList<>();

    public TerminologyDependency(String url, String version, boolean resourceExists, boolean versionExists) {
        this.url = url;
        this.version = version;
        this.resourceExists = resourceExists;
        this.versionExists = versionExists;
    }

    @Data
    @NoArgsConstructor
    @AllArgsConstructor
    public static class SourceProfile {
        private String url;
        private List<String> element = new ArrayList<>();
    }
}
