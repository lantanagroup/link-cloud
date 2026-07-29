package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.models.Semver;
import com.lantanagroup.link.validation.services.execution.executors.CustomCheckExecutor;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;


@Service
@RequiredArgsConstructor
public class RubricDefinitionValidator {

    private final CustomCheckExecutor customCheckExecutor;

    public void validate(RubricVersionPayloadDto payload) {
        List<String> errors = new ArrayList<>();

        if (payload.getId() == null || payload.getId().isBlank()) {
            errors.add("id: must not be blank");
        }
        if (!Semver.isValid(payload.getSemver())) {
            errors.add("semver: '" + payload.getSemver() + "' is not a valid semantic version (expected MAJOR.MINOR.PATCH)");
        }
        if (payload.getChecks() == null || payload.getChecks().isEmpty()) {
            errors.add("checks: at least one check is required");
            throw new InvalidRubricDefinitionException("Invalid rubric definition", errors);
        }

        Set<String> seenIds = new HashSet<>();
        for (CheckDto check : payload.getChecks()) {
            String label = check.getId() != null ? check.getId() : "<no id>";
            if (check.getId() == null || check.getId().isBlank()) {
                errors.add("checks[" + label + "]: id must not be blank");
            } else if (!seenIds.add(check.getId())) {
                errors.add("checks[" + label + "]: duplicate check id");
            }
            if (check.getType() == null) {
                errors.add("checks[" + label + "]: type is required");
                continue;
            }
            if (check.getDimension() == null) {
                errors.add("checks[" + label + "]: dimension is required");
            } else if (payload.getDimensions() != null && !payload.getDimensions().isEmpty()
                    && !payload.getDimensions().contains(check.getDimension())) {
                errors.add("checks[" + label + "]: dimension " + check.getDimension()
                        + " is not declared in the rubric's dimensions");
            }
            validateParameters(check, label, errors);
        }

        if (!errors.isEmpty()) {
            throw new InvalidRubricDefinitionException("Invalid rubric definition", errors);
        }
    }

    private void validateParameters(CheckDto check, String label, List<String> errors) {
        JsonNode params = check.getParameters();
        switch (check.getType()) {
            case FHIRPATH, COMPLETENESS, PLAUSIBILITY, CURRENCY -> {
                if (isBlank(params, "expression")) {
                    errors.add("checks[" + label + "]: " + check.getType() + " requires parameters.expression");
                }
            }
            case VALUESET -> {
                if (isBlank(params, "path")) {
                    errors.add("checks[" + label + "]: VALUESET requires parameters.path");
                }
                if (isBlank(params, "valueSet")) {
                    errors.add("checks[" + label + "]: VALUESET requires parameters.valueSet");
                }
            }
            case CUSTOM -> {
                String customCheckId = text(params, "customCheckId");
                String className = text(params, "className");
                if (customCheckId == null && className == null) {
                    errors.add("checks[" + label + "]: CUSTOM requires parameters.customCheckId or parameters.className");
                } else if (!customCheckExecutor.canResolve(customCheckId, className)) {
                    errors.add("checks[" + label + "]: no CustomCheck plug-in found for "
                            + (customCheckId != null ? "customCheckId '" + customCheckId + "'" : "className '" + className + "'"));
                }
            }
            default -> { }
        }
    }

    private boolean isBlank(JsonNode params, String field) {
        return text(params, field) == null;
    }

    private String text(JsonNode params, String field) {
        if (params == null) return null;
        String value = params.path(field).asText(null);
        return value == null || value.isBlank() ? null : value;
    }
}
