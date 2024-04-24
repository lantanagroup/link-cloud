package com.lantanagroup.link.validation.model;

import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.List;

/**
 * Model of a single category and its rule sets to be used by the API's bulk save endpoint.
 */
@Getter
@Setter
public class BulkSaveCategory {
    private String id;
    private boolean acceptable;
    private String guidance;
    private boolean requireAllRuleSets;
    private List<CategoryRuleSetModel> ruleSets = new ArrayList<>();
}
