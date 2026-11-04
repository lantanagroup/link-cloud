package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.CategorySnapshot;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.matchers.CompositeMatcher;
import com.lantanagroup.link.validation.matchers.InvertibleMatcher;
import com.lantanagroup.link.validation.matchers.Matcher;
import org.junit.jupiter.api.Test;

import java.io.InputStream;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.stream.Collectors;

import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.fail;

/**
 * Structural guards over every shipped category rule, complementing the behavioural cases in
 * {@link CategoriesJsonMatcherTest}. The defect they prevent from coming back: OR-ing an inverted
 * child. "Does not match X" is true for almost any message, so a single inverted child inside
 * {@code requiresAllChildren: false} turns the whole rule into a catch-all — and both historical
 * offenders were acceptable=true, so they silently marked unrelated findings acceptable. With
 * category override consuming these rules at evaluation time, a catch-all rule would now also
 * silently rewrite severities, so the guard matters more, not less.
 */
class CategoriesJsonStructureTest {

    private final ObjectMapper objectMapper = new ObjectMapper();

    private Map<String, Matcher> matchersById() throws Exception {
        try (InputStream stream = getClass().getClassLoader().getResourceAsStream("categories.json")) {
            assertNotNull(stream, "categories.json must be on the classpath");
            CategorySnapshot[] snapshots = objectMapper.readValue(stream, CategorySnapshot[].class);
            return Arrays.stream(snapshots)
                    .collect(Collectors.toMap(CategorySnapshot::getId, CategorySnapshot::getMatcher,
                            (a, b) -> a, LinkedHashMap::new));
        }
    }

    @Test
    void noRuleOrsAnInvertedChild() throws Exception {
        matchersById().forEach((id, matcher) -> assertNoOrOverInverted(matcher, id));
    }

    private void assertNoOrOverInverted(Matcher matcher, String categoryId) {
        if (!(matcher instanceof CompositeMatcher composite) || composite.getChildren() == null) {
            return;
        }
        if (!composite.isRequiresAllChildren()) {
            for (Matcher child : composite.getChildren()) {
                if (child instanceof InvertibleMatcher invertible && invertible.isInverted()) {
                    fail("Category '" + categoryId + "' ORs an inverted child, which matches nearly "
                            + "every finding. Wrap the positive alternatives in a nested composite and "
                            + "AND the exclusions around it.");
                }
            }
        }
        composite.getChildren().forEach(child -> assertNoOrOverInverted(child, categoryId));
    }

    @Test
    void everyRuleIsEvaluableAgainstAnEmptyResult() throws Exception {
        // Same contract the category endpoints enforce on submitted rules; the override engine
        // additionally relies on it for findings that carry no code or expression.
        matchersById().forEach((id, matcher) -> {
            assertNotNull(matcher, "category '" + id + "' has no matcher");
            try {
                matcher.isMatch(new Result());
            } catch (Exception e) {
                fail("Category '" + id + "' has an unusable matcher: " + e.getMessage());
            }
        });
    }
}
