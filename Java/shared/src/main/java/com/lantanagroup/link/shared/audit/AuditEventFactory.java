package com.lantanagroup.link.shared.audit;

import com.lantanagroup.link.shared.auth.PrincipalUser;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.core.Authentication;
import org.springframework.util.LinkedCaseInsensitiveMap;

import java.time.Clock;
import java.time.temporal.ChronoUnit;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Builds the {@link AuditEventMessage} for one request from the {@link Auditable} annotation, what the handler
 * said ({@link AuditState}), the path variables and the signed-in user. It may throw on a programming error;
 * its caller catches.
 */
public final class AuditEventFactory {
    private static final Logger logger = LoggerFactory.getLogger(AuditEventFactory.class);
    private static final Pattern PLACEHOLDER = Pattern.compile("\\{([^{}]+)\\}");

    private final AuditProperties properties;
    private final Clock clock;

    public AuditEventFactory(AuditProperties properties, Clock clock) {
        this.properties = properties;
        this.clock = clock;
    }

    public AuditEventMessage create(Auditable auditable, AuditState state,
                                    Map<String, String> pathVariables, Authentication authentication) {
        Map<String, String> variables = new LinkedCaseInsensitiveMap<>();
        if (pathVariables != null) {
            variables.putAll(pathVariables);
        }

        AuditAction action = state.actionOverride() != null ? state.actionOverride() : auditable.action();

        String resourceId = firstNonBlank(state.recordId(), variables.get(auditable.recordIdVariable()));
        if (resourceId == null) {
            logger.debug("No record id for audited resource {}", auditable.resource());
        }
        String facilityId = firstNonBlank(state.facilityId(), variables.get(auditable.facilityIdVariable()));

        List<PropertyChange> changes = action == AuditAction.UPDATE && auditable.propertyChanges()
                && !state.changes().isEmpty() ? List.copyOf(state.changes()) : null;

        return new AuditEventMessage(
                facilityId,
                properties.getServiceName(),
                null,
                clock.instant().truncatedTo(ChronoUnit.MILLIS),
                null,
                userOf(authentication),
                action,
                auditable.resource(),
                resourceId,
                state.patientId(),
                changes,
                notes(auditable.note(), state, variables));
    }

    private static String firstNonBlank(String first, String second) {
        if (first != null && !first.isBlank()) {
            return first;
        }
        return second != null && !second.isBlank() ? second : null;
    }

    private static String userOf(Authentication authentication) {
        if (authentication != null && authentication.getPrincipal() instanceof PrincipalUser principal
                && principal.getEmailAddress() != null && !principal.getEmailAddress().isBlank()) {
            return principal.getEmailAddress();
        }
        return null;
    }

    private static String notes(String template, AuditState state, Map<String, String> variables) {
        if (state.noteSuppressed() || template == null || template.isEmpty()) {
            return null;
        }
        Matcher matcher = PLACEHOLDER.matcher(template);
        StringBuilder result = new StringBuilder();
        while (matcher.find()) {
            String name = matcher.group(1);
            String replacement;
            if (state.values().containsKey(name)) {
                replacement = AuditValues.text(state.values().get(name));
            } else if (variables.containsKey(name)) {
                replacement = AuditValues.text(variables.get(name));
            } else {
                replacement = matcher.group();
            }
            matcher.appendReplacement(result, Matcher.quoteReplacement(replacement));
        }
        matcher.appendTail(result);
        return result.toString();
    }
}
