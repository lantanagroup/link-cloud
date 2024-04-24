package com.lantanagroup.link.validation.model;

import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.List;

/**
 * Model for a set of category rule sets used by the API to update the rule sets for a category.
 */
@Getter
@Setter
public class CategoryRuleSetsModel {
    private boolean requireAllRuleSets;
    private List<CategoryRuleSetModel> ruleSets = new ArrayList<>();
}
