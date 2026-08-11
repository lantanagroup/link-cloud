package com.lantanagroup.link.validation.services.categoryoverride;

import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.CategoryMatchStrategy;
import com.lantanagroup.link.validation.enums.CategoryOverrideScope;
import com.lantanagroup.link.validation.enums.CheckType;
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

@Service
@RequiredArgsConstructor
@Slf4j
public class CategoryOverrideEngine {

    private final CategorizationService categorizationService;
    private final CategorySequenceProvider sequenceProvider;
    private final ValidationPolicyConfig config;

    public List<EvaluatedFinding> apply(List<RawFinding> findings, Map<String, CheckType> checkTypeByLocalId) {
        if (findings == null || findings.isEmpty()) {
            return List.of();
        }
        ValidationPolicyConfig.CategoryOverride settings = config.getCategoryOverride();
        if (!settings.isEnabled()) {
            log.debug("Category override disabled; {} finding(s) keep their own severity", findings.size());
            return identity(findings);
        }

        CategoryOverrideScope scope = settings.getScope();
        Map<String, CheckType> checkTypes = checkTypeByLocalId != null ? checkTypeByLocalId : Map.of();

        // Index-aligned with `findings`; null means the finding is out of scope and stays untouched.
        List<Result> views = new ArrayList<>(findings.size());
        List<Result> toCategorize = new ArrayList<>(findings.size());
        for (RawFinding finding : findings) {
            if (!inScope(scope, checkTypes.get(finding.getCheckLocalId()))) {
                views.add(null);
                continue;
            }
            Result view = FindingResultAdapter.toTransientResult(finding);
            views.add(view);
            toCategorize.add(view);
        }
        if (toCategorize.isEmpty()) {
            log.debug("Category override enabled but no finding is within scope {}", scope);
            return identity(findings);
        }

        try {
            categorizationService.categorize(toCategorize);
        } catch (Exception e) {
            // A single unusable matcher in the category table must not fail the whole evaluation;
            // falling back to identity scores exactly as the disabled path would.
            log.error("Categorization failed; findings keep their own severity", e);
            return identity(findings);
        }

        CategoryMatchStrategy strategy = settings.getMatchStrategy();
        List<EvaluatedFinding> evaluated = new ArrayList<>(findings.size());
        int matchedCount = 0;
        int overriddenCount = 0;
        for (int i = 0; i < findings.size(); i++) {
            Result view = views.get(i);
            List<Category> matched = view != null ? inSequenceOrder(view.getCategories()) : List.of();
            EvaluatedFinding decision = CategoryCombiner.combine(findings.get(i), matched, strategy);
            evaluated.add(decision);
            if (decision.hasCategories()) {
                matchedCount++;
            }
            if (decision.severityWasOverridden()) {
                overriddenCount++;
            }
        }
        log.debug("Category override matched {} of {} finding(s) using {}", matchedCount, findings.size(), strategy);
        if (overriddenCount > 0) {
            log.info("Category override changed the severity of {} of {} finding(s) (scope {}, strategy {})",
                    overriddenCount, findings.size(), scope, strategy);
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

    private boolean inScope(CategoryOverrideScope scope, CheckType checkType) {
        if (scope == CategoryOverrideScope.ALL_CHECKS) {
            return true;
        }
        return checkType != null && scope.includes(checkType);
    }

    private static List<EvaluatedFinding> identity(List<RawFinding> findings) {
        return findings.stream().map(EvaluatedFinding::identity).toList();
    }
}
