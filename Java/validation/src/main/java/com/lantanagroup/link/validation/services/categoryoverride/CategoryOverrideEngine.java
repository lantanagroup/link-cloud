package com.lantanagroup.link.validation.services.categoryoverride;

import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.CategorizationService;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
@Slf4j
public class CategoryOverrideEngine {

    private final CategorizationService categorizationService;
    private final CategorySequenceProvider sequenceProvider;
    private final ValidationPolicyConfig config;

    public List<EvaluatedFinding> apply(List<RawFinding> findings) {
        if (findings == null || findings.isEmpty()) {
            return List.of();
        }
        ValidationPolicyConfig.CategoryOverride settings = config.getCategoryOverride();
        if (!settings.isEnabled()) {
            log.debug("Category override disabled; {} finding(s) keep their own severity", findings.size());
            return identity(findings);
        }

        List<Result> views = findings.stream().map(FindingResultAdapter::toTransientResult).toList();

        try {
            categorizationService.categorize(views);
        } catch (Exception e) {
            // A single unusable matcher in the category table must not fail the whole evaluation;
            // falling back to identity scores exactly as the disabled path would.
            log.error("Categorization failed; findings keep their own severity", e);
            return identity(findings);
        }

        List<EvaluatedFinding> evaluated = new ArrayList<>(findings.size());
        int matchedCount = 0;
        int overriddenCount = 0;
        for (int i = 0; i < findings.size(); i++) {
            List<Category> matched = inSequenceOrder(views.get(i).getCategories());
            EvaluatedFinding decision = CategoryCombiner.combine(findings.get(i), matched);
            evaluated.add(decision);
            if (decision.hasCategories()) {
                matchedCount++;
            }
            if (decision.severityWasOverridden()) {
                overriddenCount++;
            }
        }
        log.debug("Category override matched {} of {} finding(s)", matchedCount, findings.size());
        if (overriddenCount > 0) {
            // From->to breakdown, e.g. {ERROR->WARNING=5}, so the log shows exactly which
            // severities were overridden — comparable against the legacy path, which never overrides.
            Map<String, Long> transitions = evaluated.stream()
                    .filter(EvaluatedFinding::severityWasOverridden)
                    .collect(Collectors.groupingBy(
                            d -> d.originalSeverity() + "->" + d.effectiveSeverity(),
                            TreeMap::new, Collectors.counting()));
            log.info("Category override changed the severity of {} of {} finding(s): {}",
                    overriddenCount, findings.size(), transitions);
        } else {
            log.info("Category override changed no severities ({} of {} finding(s) matched a category)",
                    matchedCount, findings.size());
        }
        return evaluated;
    }

    private List<Category> inSequenceOrder(List<Category> matched) {
        if (matched == null || matched.isEmpty()) {
            return List.of();
        }
        return matched.stream()
                .sorted(Comparator.comparingInt((Category c) -> sequenceProvider.sequenceOf(c.getId()))
                        .thenComparing(Category::getId))
                .toList();
    }

    private static List<EvaluatedFinding> identity(List<RawFinding> findings) {
        return findings.stream().map(EvaluatedFinding::identity).toList();
    }
}
