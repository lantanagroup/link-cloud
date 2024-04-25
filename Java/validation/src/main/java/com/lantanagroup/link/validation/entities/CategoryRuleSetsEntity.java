package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.converters.CategoryRuleSetsConverter;
import com.lantanagroup.link.validation.model.CategoryRuleSetModel;
import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

import java.sql.Timestamp;
import java.util.ArrayList;
import java.util.List;

/**
 * Entity for a version of rule sets for a category.
 */
@Entity
@Table(name = "category_rulesets", uniqueConstraints = @UniqueConstraint(columnNames = {"category_id", "version"}))
@JsonInclude(JsonInclude.Include.NON_NULL)
@Getter
@Setter
public class CategoryRuleSetsEntity {
    @Id
    @GeneratedValue
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "category_id", nullable = false)
    private CategoryEntity category;

    @Column(nullable = false)
    private int version;

    @Column(nullable = false)
    private Timestamp created = new Timestamp(System.currentTimeMillis());

    private boolean requireAllRuleSets = false;

    @Convert(converter = CategoryRuleSetsConverter.class)
    @Column(columnDefinition = "varchar(max)", nullable = false)
    private List<CategoryRuleSetModel> ruleSets = new ArrayList<>();
}
