package com.lantanagroup.link.shared.audit;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.util.PatternMatchUtils;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.TreeSet;

/**
 * Which properties are masked in an audit event, and which are bookkeeping timestamps to skip. Starts from
 * the shared lists and can only grow, through {@link AuditMaskingCustomizer}s.
 */
public final class AuditMasking {
    public static final String REDACTED = "[redacted]";

    private static final Logger logger = LoggerFactory.getLogger(AuditMasking.class);

    private static final List<String> SHARED_NAMES = List.of(
            "Password", "Secret", "ClientSecret", "Token", "AccessToken", "RefreshToken", "ApiKey",
            "PrivateKey", "Passphrase", "ConnectionString", "Authentication", "Credentials");
    private static final List<String> SHARED_PATTERNS = List.of("*Secret*", "*Password*", "*Token*", "*Key");
    private static final List<String> SHARED_TIMESTAMP_NAMES = List.of(
            "ModifyDate", "ModifiedDate", "ModifiedOn", "LastModifiedOn", "UpdatedAt", "LastUpdatedDate",
            "LastUpdated");

    private final Set<String> names = new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
    private final List<String> patterns = new ArrayList<>();
    private final Set<String> timestampNames = new TreeSet<>(String.CASE_INSENSITIVE_ORDER);

    public AuditMasking(List<AuditMaskingCustomizer> customizers) {
        names.addAll(SHARED_NAMES);
        patterns.addAll(SHARED_PATTERNS);
        timestampNames.addAll(SHARED_TIMESTAMP_NAMES);

        Additions additions = new Additions(names, patterns, timestampNames);
        if (customizers != null) {
            for (AuditMaskingCustomizer customizer : customizers) {
                if (customizer != null) {
                    customizer.customize(additions);
                }
            }
        }

        logger.info("Audit masking: names={}, patterns={}, timestampNames={}", names, patterns, timestampNames);
    }

    /** True when the name is in the masked-name list or matches a masked pattern, without regard to case. */
    public boolean isMasked(String propertyName) {
        if (propertyName == null || propertyName.isEmpty()) {
            return false;
        }
        if (names.contains(propertyName)) {
            return true;
        }
        String lowerName = propertyName.toLowerCase(Locale.ROOT);
        for (String pattern : patterns) {
            if (PatternMatchUtils.simpleMatch(pattern.toLowerCase(Locale.ROOT), lowerName)) {
                return true;
            }
        }
        return false;
    }

    /** True when the name is a bookkeeping timestamp that is never reported, without regard to case. */
    public boolean isTimestamp(String propertyName) {
        return propertyName != null && timestampNames.contains(propertyName);
    }

    public Set<String> names() {
        return Collections.unmodifiableSet(names);
    }

    public List<String> patterns() {
        return Collections.unmodifiableList(patterns);
    }

    public Set<String> timestampNames() {
        return Collections.unmodifiableSet(timestampNames);
    }

    /** Add-only view of the rules handed to customizers. Blank arguments are ignored. */
    public static final class Additions {
        private final Set<String> names;
        private final List<String> patterns;
        private final Set<String> timestampNames;

        private Additions(Set<String> names, List<String> patterns, Set<String> timestampNames) {
            this.names = names;
            this.patterns = patterns;
            this.timestampNames = timestampNames;
        }

        public Additions addName(String name) {
            if (!isBlank(name)) {
                names.add(name.trim());
            }
            return this;
        }

        public Additions addPattern(String pattern) {
            if (!isBlank(pattern)) {
                patterns.add(pattern.trim());
            }
            return this;
        }

        public Additions addTimestampName(String name) {
            if (!isBlank(name)) {
                timestampNames.add(name.trim());
            }
            return this;
        }
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }
}
