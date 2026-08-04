package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.JsonNode;
import org.springframework.stereotype.Component;

import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Validates the declarative {@code applicableContext} block of a rubric definition
 * (see Rubric JSON Field Reference §3): where/when the rubric is meant to be applied.
 * Allowed keys are exactly {@code fhirResources} (must be real FHIR R4 resource type
 * names, verified against the FhirContext) and {@code workflowTags} (bounded by the
 * {@code rubric_result.workflow_tag varchar(128)} column they will flow into).
 */
@Component
public class ApplicableContextValidator {

    private static final Set<String> ALLOWED_KEYS = Set.of("fhirResources", "workflowTags");
    private static final int MAX_ENTRIES = 50;
    private static final int MAX_WORKFLOW_TAG_LENGTH = 128;

    private final Set<String> fhirResourceTypes;

    public ApplicableContextValidator(FhirContext fhirContext) {
        this.fhirResourceTypes = Set.copyOf(fhirContext.getResourceTypes());
    }

    public void validate(JsonNode applicableContext, List<String> errors) {
        if (applicableContext == null || applicableContext.isNull()) {
            return;
        }
        if (!applicableContext.isObject()) {
            errors.add("applicableContext: must be a JSON object");
            return;
        }

        applicableContext.fieldNames().forEachRemaining(key -> {
            if (!ALLOWED_KEYS.contains(key)) {
                errors.add("applicableContext: unknown property '" + key + "'");
            }
        });

        JsonNode resources = applicableContext.get("fhirResources");
        if (resources != null) {
            validateStringArray(resources, "applicableContext.fhirResources", Integer.MAX_VALUE, errors,
                    (value, path) -> {
                        if (!fhirResourceTypes.contains(value)) {
                            errors.add(path + ": '" + value + "' is not a valid FHIR R4 resource type");
                        }
                    });
        }

        JsonNode tags = applicableContext.get("workflowTags");
        if (tags != null) {
            validateStringArray(tags, "applicableContext.workflowTags", MAX_WORKFLOW_TAG_LENGTH, errors,
                    (value, path) -> { });
        }
    }

    private interface ElementCheck {
        void check(String value, String path);
    }

    private void validateStringArray(JsonNode node, String path, int maxLength,
                                     List<String> errors, ElementCheck elementCheck) {
        if (!node.isArray()) {
            errors.add(path + ": must be an array of strings");
            return;
        }
        if (node.isEmpty()) {
            errors.add(path + ": must not be empty when present");
            return;
        }
        if (node.size() > MAX_ENTRIES) {
            errors.add(path + ": at most " + MAX_ENTRIES + " entries are allowed");
            return;
        }
        Set<String> seen = new HashSet<>();
        for (int i = 0; i < node.size(); i++) {
            JsonNode element = node.get(i);
            String elementPath = path + "[" + i + "]";
            if (!element.isTextual() || element.asText().isBlank()) {
                errors.add(elementPath + ": must be a non-blank string");
                continue;
            }
            String value = element.asText();
            if (value.length() > maxLength) {
                errors.add(elementPath + ": must be at most " + maxLength + " characters");
                continue;
            }
            if (!seen.add(value)) {
                errors.add(path + ": duplicate value '" + value + "'");
                continue;
            }
            elementCheck.check(value, elementPath);
        }
    }
}
