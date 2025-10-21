package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategorySnapshot;

import java.io.IOException;
import java.io.InputStream;
import java.util.Arrays;
import java.util.List;

/**
 * Shared test fixtures for the categorization tests.
 */
final class CategoryFixtures {

    private CategoryFixtures() {
    }

    /**
     * Loads the categories that actually ship in categories.json so tests exercise the real matchers
     * rather than a hand-copied regex (guards against drift between the shipped category and the
     * message the validation service emits). Each Category is constructed with its single CategoryRule
     * exactly as the production snapshot mapping does.
     */
    static List<Category> loadShippedCategories() throws IOException {
        ObjectMapper mapper = new ObjectMapper();
        try (InputStream stream = Thread.currentThread().getContextClassLoader().getResourceAsStream("categories.json")) {
            CategorySnapshot[] snapshots = mapper.readValue(stream, CategorySnapshot[].class);
            return Arrays.stream(snapshots)
                    .map(snapshot -> {
                        Category category = snapshot.toCategory();
                        category.setRules(List.of(snapshot.toCategoryRule(category)));
                        return category;
                    })
                    .toList();
        }
    }
}
