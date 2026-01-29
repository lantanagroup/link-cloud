package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class TerminologyDependency {
    private String url;
    private String version;
    private boolean resourceExists;
    private boolean versionExists;
}
