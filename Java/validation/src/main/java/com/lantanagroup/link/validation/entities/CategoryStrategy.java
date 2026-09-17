package com.lantanagroup.link.validation.entities;

/**
 * Declares at which validation lifecycle stage a {@link Category}'s matcher takes effect.
 *
 * <ul>
 *   <li>{@link #SKIP} — implemented as an {@code IValidationPolicyAdvisor} pre-validation decision
 *       (e.g. {@code policyForCodedContent}); the underlying check is never performed and the
 *       associated message is never produced. Real CPU savings. Requires a non-null
 *       {@link CategoryScope} so the advisor knows when the rule applies.</li>
 *   <li>{@link #SUPPRESS} — implemented as {@code isSuppressMessageId(...)}; the check runs but
 *       the message is dropped before being added to the {@code OperationOutcome}. Cleaner output
 *       but no CPU savings.</li>
 *   <li>{@link #LABEL} — default; the existing post-validation behaviour. {@code CategorizationService}
 *       matches the rule against produced {@link Result} records and tags them with the category.</li>
 * </ul>
 *
 * <p>Rules without an explicit strategy default to {@link #LABEL} so adding the field is a pure
 * shape change with no behaviour change for the existing rule set.</p>
 *
 * <p>{@link #SKIP} and {@link #SUPPRESS} are not legal for categories with {@code acceptable=false}
 * — silently hiding a blocking error would undermine the report's submission gating.
 * Enforcement of that constraint lives on {@link Category} itself.</p>
 */
public enum CategoryStrategy {
    SKIP,
    SUPPRESS,
    LABEL
}
