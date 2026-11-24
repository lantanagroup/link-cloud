package com.lantanagroup.link.shared.audit;

/**
 * Lets a service add property names, patterns and timestamp names to the shared audit masking rules. A
 * customizer can only add; the shared entries cannot be removed. Patterns support the {@code *} wildcard only
 * (the .NET side also supports {@code ?}).
 */
@FunctionalInterface
public interface AuditMaskingCustomizer {
    void customize(AuditMasking.Additions additions);
}
