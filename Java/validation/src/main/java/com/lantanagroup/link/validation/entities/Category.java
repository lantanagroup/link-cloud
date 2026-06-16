package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.converters.CategoryScopeConverter;
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
