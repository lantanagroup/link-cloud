package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.ArrayList;
import java.util.List;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class PackageDetailsModel {
    private String version;
    private String id;
    private String name;
    private List<Resource> resources = new ArrayList<>();

    @Data
    @NoArgsConstructor
    @AllArgsConstructor
    public static class Resource {
        private String resourceType;
        private String id;
        private String url;
        private String name;
        private String version;
    }
}
