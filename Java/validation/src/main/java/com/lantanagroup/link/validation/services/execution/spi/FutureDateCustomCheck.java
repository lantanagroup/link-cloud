package com.lantanagroup.link.validation.services.execution.spi;

import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.shared.utils.LogUtils;
import lombok.RequiredArgsConstructor;
import org.hl7.fhir.instance.model.api.IBase;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.DateType;
import org.hl7.fhir.r4.model.InstantType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;

/**
 * Flags any date/dateTime/instant value (located via a FHIRPath expression) that lies
 * in the future.  This is a plausibility check — recorded clinical dates should not
 * be later than the evaluation timestamp.
 * <p>
 * Required parametersJson fields:
 *   "path"           – FHIRPath expression selecting the date nodes to inspect
 * <p>
 * Optional parametersJson fields:
 *   "code"           – finding code (default: "future-date")
 *   "failureMessage" – human-readable message prefix (default: "Date at <path> must not be in the future")
 */
@Component
@RequiredArgsConstructor
public class FutureDateCustomCheck implements CustomCheck {

    private static final Logger logger = LoggerFactory.getLogger(FutureDateCustomCheck.class);

    private final IFhirPath fhirPath;
    private final ObjectMapper objectMapper;

    @Override
    public String id() {
        return "future-date";
    }

    @Override
    public List<RawFinding> run(RubricCheck check, ExecutionContext context) {
        if (check.getParametersJson() == null) return List.of();

        final JsonNode params;
        try {
            params = objectMapper.readTree(check.getParametersJson());
        } catch (Exception e) {
            logger.warn("future-date check {} has invalid parameters JSON", LogUtils.sanitize(check.getCheckLocalId()));
            return List.of();
        }

        String path = params.path("path").asText(null);
        if (path == null || path.isBlank()) {
            logger.warn("future-date check {} missing required 'path' parameter", LogUtils.sanitize(check.getCheckLocalId()));
            return List.of();
        }

        String code = params.path("code").asText("future-date");
        String failureMessage = params.path("failureMessage")
                .asText("Date at " + path + " must not be in the future");
        Severity severity = check.getSeverityOverride() != null
                ? check.getSeverityOverride()
                : Severity.WARNING;

        List<IBaseResource> bundleEntries = context.getBundleEntries();
        List<IBaseResource> targets = !bundleEntries.isEmpty() ? bundleEntries
                : context.getResource() != null ? List.of(context.getResource()) : List.of();
        if (targets.isEmpty()) return List.of();

        Instant now = Instant.now();
        List<RawFinding> findings = new ArrayList<>();

        for (IBaseResource resource : targets) {
            List<IBase> nodes;
            try {
                nodes = fhirPath.evaluate(resource, path, IBase.class);
            } catch (Exception e) {
                logger.debug("future-date path '{}' did not evaluate on {}: {}",
                        LogUtils.sanitize(path), resource.fhirType(), LogUtils.sanitize(e.getMessage()));
                continue;
            }

            for (IBase node : nodes) {
                Instant instant = toInstant(node);
                if (instant == null) continue;
                if (isFuture(node, instant, now)) {
                    findings.add(RawFinding.builder()
                            .checkLocalId(check.getCheckLocalId())
                            .dimension(check.getDimension())
                            .severity(severity)
                            .code(code)
                            .message(String.format("%s (observed %s)", failureMessage, instant))
                            .location(resource.fhirType())
                            .expression(path)
                            .build());
                }
            }
        }
        return findings;
    }

    // dates are day-precision with no zone, so compare calendar days in UTC rather than instants
    private boolean isFuture(IBase node, Instant instant, Instant now) {
        if (node instanceof DateType) {
            LocalDate date = instant.atZone(ZoneOffset.UTC).toLocalDate();
            return date.isAfter(now.atZone(ZoneOffset.UTC).toLocalDate());
        }
        return instant.isAfter(now);
    }

    private Instant toInstant(IBase node) {
        if (node instanceof DateTimeType dt) return dt.getValue() != null ? dt.getValue().toInstant() : null;
        if (node instanceof InstantType it) return it.getValue() != null ? it.getValue().toInstant() : null;
        if (node instanceof DateType d) return d.getValue() != null ? d.getValue().toInstant() : null;
        return null;
    }
}
