package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.converters.CategoryScopeConverter;
import com.lantanagroup.link.validation.converters.SuppressMessageIdsConverter;
import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

import java.util.Comparator;
import java.util.List;

@Getter
@Setter
@Entity
@JsonInclude(JsonInclude.Include.NON_NULL)
public class Category {
    public static final Category UNCATEGORIZED;

    static {
        UNCATEGORIZED = new Category();
        UNCATEGORIZED.setId("uncategorized");
        UNCATEGORIZED.setTitle("Uncategorized");
        UNCATEGORIZED.setSeverity(CategorySeverity.WARNING);
        UNCATEGORIZED.setAcceptable(false);
        UNCATEGORIZED.setGuidance("These issues need to be categorized.");
    }

    @Id
    private String id;

    @Column(nullable = false)
    private String title;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private CategorySeverity severity;

    @Column(nullable = false)
    private boolean acceptable;

    @Column(length = 1000, nullable = false)
    private String guidance;

    /**
     * When during validation this category's matcher takes effect. See {@link CategoryStrategy}.
     * Defaults to {@link CategoryStrategy#LABEL} so adding the field is backward-compatible
     * for existing rule data that doesn't carry it.
     */
    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 50,
            columnDefinition = "varchar(50) default 'LABEL' not null")
    private CategoryStrategy strategy = CategoryStrategy.LABEL;

    /**
     * Scoping metadata for {@link CategoryStrategy#SKIP} rules. Nullable for
     * {@code LABEL} / {@code SUPPRESS}. See {@link CategoryScope}.
     */
    @Convert(converter = CategoryScopeConverter.class)
    @Column(columnDefinition = "varchar(max)")
    private CategoryScope scope;

    /**
     * Stable HAPI message IDs (constants from
     * {@code org.hl7.fhir.utilities.i18n.I18nConstants}) that this rule should suppress.
     * Wired into {@code CategoryBackedPolicyAdvisor.isSuppressMessageId(...)} — when the
     * validator is about to emit a message with one of these IDs, the advisor returns
     * {@code true} and the message is dropped before reaching the {@code OperationOutcome}.
     *
     * <p>Independent of {@link #strategy} — a rule can carry both {@link #scope} (for SKIP)
     * and {@code suppressMessageIds} (for SUPPRESS) on the same rule. The advisor wires up
     * whichever fields are present. {@code strategy} stays as a human-readable hint for the
     * rule's primary intent; the actual hooks fire based on field presence.</p>
     *
     * <p>Nullable. {@link SuppressMessageIdsConverter} maps null / empty lists to a null DB
     * column rather than the literal string {@code "null"} or {@code "[]"}.</p>
     */
    @Convert(converter = SuppressMessageIdsConverter.class)
    @Column(columnDefinition = "varchar(max)")
    private List<String> suppressMessageIds;

    /**
     * Optional path-narrowing for {@link #suppressMessageIds}. Regex patterns matched against the
     * {@code path} argument that HAPI's validator passes to
     * {@code CategoryBackedPolicyAdvisor.isSuppressMessageId(...)}. When non-empty, the rule only
     * fires when at least one pattern matches the path — letting us suppress a globally-named
     * message ID at specific elements only (e.g. {@code Reference_REF_NoDisplay} on
     * {@code MedicationRequest.requester} but not on every Reference element in every resource).
     *
     * <p>When null or empty, the rule fires on any path that produces one of its
     * {@link #suppressMessageIds} — Phase 4's default behaviour. Reuses
     * {@link SuppressMessageIdsConverter} since the column shape is the same JSON-array-of-strings.</p>
     */
    @Convert(converter = SuppressMessageIdsConverter.class)
    @Column(columnDefinition = "varchar(max)")
    private List<String> suppressPathPatterns;

    @OneToMany(fetch = FetchType.EAGER, mappedBy = "category")
    @JsonIgnore
    private List<CategoryRule> rules;

    @JsonIgnore
    public CategoryRule getLatestRule() {
        List<CategoryRule> rules = getRules();
        if (rules == null) {
            return null;
        }
        return rules.stream()
                .max(Comparator.comparing(CategoryRule::getTimestamp))
                .orElse(null);
    }
}
