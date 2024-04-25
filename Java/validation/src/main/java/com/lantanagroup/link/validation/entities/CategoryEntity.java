package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonInclude;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.Getter;
import lombok.Setter;

@Entity
@Table(name = "category")
@JsonInclude(JsonInclude.Include.NON_NULL)
@Getter
@Setter
public class CategoryEntity {
    @Id
    private String id;

    @Column(nullable = false)
    private boolean acceptable;

    @Column(nullable = false)
    private String guidance;

    private Boolean requireAllRuleSets = false;
}
