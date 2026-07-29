package com.lantanagroup.link.validation.services.execution.spi;

import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.hl7.fhir.instance.model.api.IBase;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.instance.model.api.IPrimitiveType;
import org.hl7.fhir.r4.model.DecimalType;
import org.hl7.fhir.r4.model.IntegerType;
import org.hl7.fhir.r4.model.Quantity;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;

@Component
@RequiredArgsConstructor
@Slf4j
public class NumericRangeCustomCheck implements CustomCheck {

    private final IFhirPath fhirPath;
    private final ObjectMapper objectMapper;

    @Override
    public String id() {
        return "numeric-range";
    }

    @Override
    public List<RawFinding> run(RubricCheck check, ExecutionContext context) {
        if (check.getParametersJson() == null) return List.of();
        final JsonNode params;
        try {
            params = objectMapper.readTree(check.getParametersJson());
        } catch (Exception e) {
            log.warn("numeric-range check {} has invalid parameters JSON", check.getCheckLocalId());
            return List.of();
        }
        String path = params.path("path").asText(null);
        if (path == null || path.isBlank()) {
            log.warn("numeric-range check {} missing 'path'", check.getCheckLocalId());
            return List.of();
        }
        Double min = params.hasNonNull("min") ? params.get("min").asDouble() : null;
        Double max = params.hasNonNull("max") ? params.get("max").asDouble() : null;
        String code = params.path("code").asText("numeric-out-of-range");
        String failureMessage = params.path("failureMessage").asText("Value at " + path + " is outside the plausible range");
        Severity severity = check.getSeverityOverride() != null ? check.getSeverityOverride() : Severity.WARNING;

        List<IBaseResource> targets = context.getBundleEntries().isEmpty()
                ? List.of(context.getResource())
                : context.getBundleEntries();

        List<RawFinding> findings = new ArrayList<>();
        for (IBaseResource resource : targets) {
            List<IBase> nodes;
            try {
                nodes = fhirPath.evaluate(resource, path, IBase.class);
            } catch (Exception e) {
                log.debug("numeric-range path '{}' did not evaluate on {}: {}", path, resource.fhirType(), e.getMessage());
                continue;
            }
            for (IBase node : nodes) {
                Double value = numericValue(node);
                if (value == null) continue;
                if ((min != null && value < min) || (max != null && value > max)) {
                    findings.add(RawFinding.builder()
                            .checkLocalId(check.getCheckLocalId())
                            .dimension(check.getDimension())
                            .severity(severity)
                            .code(code)
                            .message(String.format("%s (observed %s)", failureMessage, value))
                            .location(resource.fhirType())
                            .expression(path)
                            .build());
                }
            }
        }
        return findings;
    }

    private Double numericValue(IBase node) {
        if (node instanceof Quantity q) return q.getValue() != null ? q.getValue().doubleValue() : null;
        if (node instanceof DecimalType d) return d.getValue() != null ? d.getValue().doubleValue() : null;
        if (node instanceof IntegerType i) return i.getValue() != null ? i.getValue().doubleValue() : null;
        if (node instanceof IPrimitiveType<?> primitive) {
            try { return Double.parseDouble(primitive.getValueAsString()); }
            catch (NumberFormatException e) { return null; }
        }
        return null;
    }
}
