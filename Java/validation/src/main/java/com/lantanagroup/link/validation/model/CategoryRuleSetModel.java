package com.lantanagroup.link.validation.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.List;

/**
 * A single set of rules for a category.
 */
@Getter
@Setter
@JsonInclude(JsonInclude.Include.NON_NULL)
public class CategoryRuleSetModel {
    private List<CategoryRuleModel> rules = new ArrayList<>();
    private boolean requireAllRules = true;
}
