package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.models.Semver;
import com.lantanagroup.link.validation.services.execution.executors.CustomCheckExecutor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.net.URI;
import java.util.ArrayList;
import java.util.EnumSet;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.regex.Pattern;
import java.util.regex.PatternSyntaxException;


@Service
public class RubricDefinitionValidator {

    private static final int MAX_CODE_LENGTH = 128;
    private static final int MAX_PROFILES = 20;

    // Allowed parameter keys per check type (Rubric JSON Field Reference §6); unknown keys rejected.
    private static final Map<CheckType, Set<String>> ALLOWED_PARAMETER_KEYS = Map.of(
            CheckType.FHIR_CONFORMANCE, Set.of("profiles"),
            CheckType.FHIRPATH, Set.of("expression", "failureMessage", "code"),
            CheckType.COMPLETENESS, Set.of("expression", "failureMessage", "code"),
            CheckType.PLAUSIBILITY, Set.of("expression", "failureMessage", "code"),
            CheckType.CURRENCY, Set.of("expression", "failureMessage", "code"),
            CheckType.TERMINOLOGY, Set.of("validateCodings", "valueSetWhitelistRegex"),
            CheckType.VALUESET, Set.of("path", "valueSet", "system"),
            CheckType.CUSTOM, Set.of("customCheckId", "className", "path", "min", "max", "code", "failureMessage")
    );

    // Self-named types must use their namesake dimension; FHIRPATH/CUSTOM are dimension-agnostic.
    private static final Map<CheckType, PiqiDimension> REQUIRED_DIMENSION_BY_TYPE = Map.of(
            CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE,
            CheckType.TERMINOLOGY, PiqiDimension.TERMINOLOGY,
            CheckType.VALUESET, PiqiDimension.TERMINOLOGY,
            CheckType.COMPLETENESS, PiqiDimension.COMPLETENESS,
            CheckType.PLAUSIBILITY, PiqiDimension.PLAUSIBILITY,
            CheckType.CURRENCY, PiqiDimension.CURRENCY
    );

    private final CustomCheckExecutor customCheckExecutor;
    private final IFhirPath fhirPath;
    private final ScoringPolicyValidator scoringPolicyValidator;
    private final ApplicableContextValidator applicableContextValidator;
    private final int maxChecks;

    public RubricDefinitionValidator(CustomCheckExecutor customCheckExecutor,
                                     IFhirPath fhirPath,
                                     ScoringPolicyValidator scoringPolicyValidator,
                                     ApplicableContextValidator applicableContextValidator,
                                     @Value("${link.rubric.max-checks:200}") int maxChecks) {
        this.customCheckExecutor = customCheckExecutor;
        this.fhirPath = fhirPath;
        this.scoringPolicyValidator = scoringPolicyValidator;
        this.applicableContextValidator = applicableContextValidator;
        this.maxChecks = maxChecks;
    }

    public void validate(RubricVersionPayloadDto payload) {
        List<String> errors = new ArrayList<>();

        if (payload.getId() == null || payload.getId().isBlank()) {
            errors.add("id: must not be blank");
        }
        if (!Semver.isValid(payload.getSemver())) {
            errors.add("semver: '" + payload.getSemver() + "' is not a valid semantic version (expected MAJOR.MINOR.PATCH)");
        }

        validateDimensions(payload.getDimensions(), errors);

        if (payload.getChecks() == null || payload.getChecks().isEmpty()) {
            errors.add("checks: at least one check is required");
            throw new InvalidRubricDefinitionException("Invalid rubric definition", errors);
        }
        if (payload.getChecks().size() > maxChecks) {
            errors.add("checks: at most " + maxChecks + " checks are allowed (got " + payload.getChecks().size() + ")");
        }
        if (payload.getChecks().stream().noneMatch(CheckDto::isEnabled)) {
            errors.add("checks: at least one check must be enabled");
        }

        Set<String> seenIds = new HashSet<>();
        Set<Integer> seenOrdinals = new HashSet<>();
        for (CheckDto check : payload.getChecks()) {
            String label = check.getId() != null ? check.getId() : "<no id>";
            if (check.getId() == null || check.getId().isBlank()) {
                errors.add("checks[" + label + "]: id must not be blank");
            } else if (!seenIds.add(check.getId())) {
                errors.add("checks[" + label + "]: duplicate check id");
            }
            if (check.getOrdinal() < 0) {
                errors.add("checks[" + label + "]: ordinal must be >= 0");
            } else if (!seenOrdinals.add(check.getOrdinal())) {
                // execution order is by ordinal (getChecks orders by it); duplicates make it nondeterministic
                errors.add("checks[" + label + "]: duplicate ordinal " + check.getOrdinal());
            }
            if (check.getSeverityOverride() == null) {
                errors.add("checks[" + label + "]: severityOverride is required");
            }
            if (check.getType() == null) {
                errors.add("checks[" + label + "]: type is required");
                continue;
            }
            if (check.getDimension() == null) {
                errors.add("checks[" + label + "]: dimension is required");
            } else {
                if (payload.getDimensions() != null && !payload.getDimensions().contains(check.getDimension())) {
                    errors.add("checks[" + label + "]: dimension " + check.getDimension()
                            + " is not declared in the rubric's dimensions");
                }
                PiqiDimension required = REQUIRED_DIMENSION_BY_TYPE.get(check.getType());
                if (required != null && check.getDimension() != required) {
                    errors.add("checks[" + label + "]: type " + check.getType()
                            + " must use dimension " + required + ", got " + check.getDimension());
                }
            }
            validateParameters(check, label, errors);
        }

        scoringPolicyValidator.validate(payload.getScoringPolicy(), errors);
        applicableContextValidator.validate(payload.getApplicableContext(), errors);

        if (!errors.isEmpty()) {
            throw new InvalidRubricDefinitionException("Invalid rubric definition", errors);
        }
    }

    private void validateDimensions(List<PiqiDimension> dimensions, List<String> errors) {
        if (dimensions == null || dimensions.isEmpty()) {
            errors.add("dimensions: at least one dimension is required");
            return;
        }
        // immutable List.of collections throw NPE on contains(null), so probe by iteration
        boolean nullReported = false;
        Set<PiqiDimension> seen = EnumSet.noneOf(PiqiDimension.class);
        for (PiqiDimension dimension : dimensions) {
            if (dimension == null) {
                if (!nullReported) {
                    errors.add("dimensions: must not contain null entries");
                    nullReported = true;
                }
            } else if (!seen.add(dimension)) {
                errors.add("dimensions: duplicate dimension " + dimension);
            }
        }
    }

    private void validateParameters(CheckDto check, String label, List<String> errors) {
        JsonNode params = check.getParameters();
        if (params != null && !params.isNull() && !params.isObject()) {
            errors.add("checks[" + label + "].parameters: must be a JSON object");
            return;
        }
        rejectUnknownParameterKeys(check.getType(), params, label, errors);

        switch (check.getType()) {
            case FHIRPATH, COMPLETENESS, PLAUSIBILITY, CURRENCY -> {
                String expression = text(params, "expression");
                if (expression == null) {
                    errors.add("checks[" + label + "]: " + check.getType() + " requires parameters.expression");
                } else {
                    validateFhirPath(expression, "checks[" + label + "].parameters.expression", errors);
                }
                validateCodeLength(params, label, errors);
            }
            case FHIR_CONFORMANCE -> validateProfiles(params, label, errors);
            case TERMINOLOGY -> {
                JsonNode validateCodings = params != null ? params.get("validateCodings") : null;
                if (validateCodings != null && !validateCodings.isBoolean()) {
                    errors.add("checks[" + label + "].parameters.validateCodings: must be a boolean");
                }
                String regex = text(params, "valueSetWhitelistRegex");
                if (regex != null) {
                    try {
                        Pattern.compile(regex);
                    } catch (PatternSyntaxException e) {
                        errors.add("checks[" + label + "].parameters.valueSetWhitelistRegex: invalid regular expression: "
                                + firstLine(e.getMessage()));
                    }
                }
            }
            case VALUESET -> {
                String path = text(params, "path");
                if (path == null) {
                    errors.add("checks[" + label + "]: VALUESET requires parameters.path");
                } else {
                    validateFhirPath(path, "checks[" + label + "].parameters.path", errors);
                }
                String valueSet = text(params, "valueSet");
                if (valueSet == null) {
                    errors.add("checks[" + label + "]: VALUESET requires parameters.valueSet");
                } else if (!isCanonicalUrl(valueSet)) {
                    errors.add("checks[" + label + "].parameters.valueSet: must be a canonical URL (absolute URI, optionally versioned with |version)");
                }
                String system = text(params, "system");
                if (system != null && !isCanonicalUrl(system)) {
                    errors.add("checks[" + label + "].parameters.system: must be a canonical URL (absolute URI, optionally versioned with |version)");
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
                String path = text(params, "path");
                if (path != null) {
                    validateFhirPath(path, "checks[" + label + "].parameters.path", errors);
                }
                validateMinMax(params, label, errors);
                validateCodeLength(params, label, errors);
            }
            default -> { }
        }
    }

    private void rejectUnknownParameterKeys(CheckType type, JsonNode params, String label, List<String> errors) {
        if (params == null || !params.isObject()) {
            return;
        }
        Set<String> allowed = ALLOWED_PARAMETER_KEYS.get(type);
        if (allowed == null) {
            return;
        }
        params.fieldNames().forEachRemaining(key -> {
            if (!allowed.contains(key)) {
                errors.add("checks[" + label + "].parameters: unknown property '" + key + "'");
            }
        });
    }

    private void validateFhirPath(String expression, String path, List<String> errors) {
        try {
            fhirPath.parse(expression);
        } catch (Exception e) {
            errors.add(path + ": invalid FHIRPath expression: " + firstLine(e.getMessage()));
        }
    }

    private void validateProfiles(JsonNode params, String label, List<String> errors) {
        JsonNode profiles = params != null ? params.get("profiles") : null;
        if (profiles == null) {
            return;
        }
        if (!profiles.isArray()) {
            errors.add("checks[" + label + "].parameters.profiles: must be an array of canonical URLs");
            return;
        }
        if (profiles.isEmpty()) {
            errors.add("checks[" + label + "].parameters.profiles: must not be empty when present");
            return;
        }
        if (profiles.size() > MAX_PROFILES) {
            errors.add("checks[" + label + "].parameters.profiles: at most " + MAX_PROFILES + " profiles are allowed");
            return;
        }
        Set<String> seen = new HashSet<>();
        for (int i = 0; i < profiles.size(); i++) {
            JsonNode profile = profiles.get(i);
            if (!profile.isTextual() || !isCanonicalUrl(profile.asText())) {
                errors.add("checks[" + label + "].parameters.profiles[" + i
                        + "]: must be a canonical URL (absolute URI, optionally versioned with |version)");
            } else if (!seen.add(profile.asText())) {
                errors.add("checks[" + label + "].parameters.profiles: duplicate profile '" + profile.asText() + "'");
            }
        }
    }

    private void validateMinMax(JsonNode params, String label, List<String> errors) {
        JsonNode min = params != null ? params.get("min") : null;
        JsonNode max = params != null ? params.get("max") : null;
        boolean minValid = true;
        boolean maxValid = true;
        if (min != null && !min.isNumber()) {
            errors.add("checks[" + label + "].parameters.min: must be a number");
            minValid = false;
        }
        if (max != null && !max.isNumber()) {
            errors.add("checks[" + label + "].parameters.max: must be a number");
            maxValid = false;
        }
        if (min != null && max != null && minValid && maxValid && min.asDouble() > max.asDouble()) {
            errors.add("checks[" + label + "].parameters: min (" + min.asDouble()
                    + ") must be <= max (" + max.asDouble() + ")");
        }
    }

    private void validateCodeLength(JsonNode params, String label, List<String> errors) {
        // findings persist to rubric_finding.code varchar(128)
        String code = text(params, "code");
        if (code != null && code.length() > MAX_CODE_LENGTH) {
            errors.add("checks[" + label + "].parameters.code: must be at most " + MAX_CODE_LENGTH + " characters");
        }
    }

    // FHIR canonical URLs may carry a version suffix: http://host/ValueSet/x|1.0.0
    private boolean isCanonicalUrl(String value) {
        String[] parts = value.split("\\|", -1);
        if (parts.length > 2) {
            return false;
        }
        if (parts.length == 2 && parts[1].isBlank()) {
            return false;
        }
        try {
            URI uri = URI.create(parts[0]);
            return uri.isAbsolute();
        } catch (IllegalArgumentException e) {
            return false;
        }
    }

    private String firstLine(String message) {
        if (message == null) {
            return "unparseable";
        }
        int newline = message.indexOf('\n');
        return newline >= 0 ? message.substring(0, newline).trim() : message.trim();
    }

    private String text(JsonNode params, String field) {
        if (params == null) return null;
        String value = params.path(field).asText(null);
        return value == null || value.isBlank() ? null : value;
    }
}
