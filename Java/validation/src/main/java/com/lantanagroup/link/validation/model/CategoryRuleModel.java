package com.lantanagroup.link.validation.model;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.Getter;
import lombok.Setter;

import java.util.regex.Pattern;

/**
 * A single rule within a set of rules for a category.
 */
@Getter
@Setter
@JsonInclude(JsonInclude.Include.NON_NULL)
public class CategoryRuleModel {
    private Fields field;
    private String regex;
    private boolean isInverse = false;

    @JsonIgnore
    private Pattern pattern;

    public static CategoryRuleModel createCategoryRuleModel(Fields field, String regex, boolean isInverse) {
        CategoryRuleModel model = createCategoryRuleModel(field, regex);
        model.isInverse = isInverse;
        return model;
    }

    public static CategoryRuleModel createCategoryRuleModel(Fields field, String regex) {
        CategoryRuleModel model = new CategoryRuleModel();
        model.field = field;
        model.regex = regex;
        return model;
    }

    public Pattern getPattern() {
        if (this.pattern == null) {
            this.pattern = Pattern.compile(this.regex, Pattern.MULTILINE);
        }
        return this.pattern;
    }

    public enum Fields {
        DETAILS_TEXT,
        EXPRESSION,
        SEVERITY,
        CODE
    }
}
