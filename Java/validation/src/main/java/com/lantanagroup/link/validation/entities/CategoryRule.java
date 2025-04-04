package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.converters.MatcherConverter;
import com.lantanagroup.link.validation.matchers.Matcher;
import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

import java.time.OffsetDateTime;

@Getter
@Setter
@Entity
@JsonInclude(JsonInclude.Include.NON_NULL)
public class CategoryRule {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(optional = false)
    @JoinColumn(foreignKey = @ForeignKey(name = "fk_category_rule_category_id"))
    @JsonIgnore
    private Category category;

    @Convert(converter = MatcherConverter.class)
    @Column(columnDefinition = "varchar(max)", nullable = false)
    private Matcher matcher;

    @Column(nullable = false)
    private OffsetDateTime timestamp = OffsetDateTime.now();
}
