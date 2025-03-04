package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.validation.matchers.Matcher;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class CategorySnapshot {
    private String id;
    private String title;
    private CategorySeverity severity;
    private boolean acceptable;
    private String guidance;
    private Matcher matcher;

    public CategorySnapshot() {
    }

    public CategorySnapshot(Category category) {
        id = category.getId();
        title = category.getTitle();
        severity = category.getSeverity();
        acceptable = category.isAcceptable();
        guidance = category.getGuidance();
        CategoryRule latestRule = category.getLatestRule();
        if (latestRule != null) {
            matcher = latestRule.getMatcher();
        }
    }

    public Category toCategory() {
        return toCategory(new Category());
    }

    public Category toCategory(Category category) {
        category.setId(id);
        category.setTitle(title);
        category.setSeverity(severity);
        category.setAcceptable(acceptable);
        category.setGuidance(guidance);
        return category;
    }

    public CategoryRule toCategoryRule() {
        return toCategoryRule(toCategory());
    }

    public CategoryRule toCategoryRule(Category category) {
        CategoryRule categoryRule = new CategoryRule();
        categoryRule.setCategory(category);
        categoryRule.setMatcher(matcher);
        return categoryRule;
    }
}
