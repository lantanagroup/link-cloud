package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.configs.ValidationResultIgnoreRuleConfig;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.matchers.CompositeMatcher;
import com.lantanagroup.link.validation.matchers.Matcher;
import com.lantanagroup.link.validation.matchers.RegexMatcher;
import org.apache.commons.collections4.CollectionUtils;
import org.apache.commons.lang3.StringUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.List;

@Service
public class ValidationResultIgnoreService {
    private static final Logger logger = LoggerFactory.getLogger(ValidationResultIgnoreService.class);

    private final List<CompiledValidationResultIgnoreRule> rules;

    public ValidationResultIgnoreService(LinkConfig linkConfig) {
        List<ValidationResultIgnoreRuleConfig> configuredRules = linkConfig.getValidationResultIgnoreRules();
        this.rules = configuredRules == null
                ? List.of()
                : configuredRules.stream()
                .map(this::compileRule)
                .toList();
    }

    public List<Result> filterIgnored(List<Result> results) {
        if (CollectionUtils.isEmpty(results) || CollectionUtils.isEmpty(rules)) {
            return results;
        }

        List<Result> filtered = new ArrayList<>(results.size());
        int ignoredCount = 0;
        for (Result result : results) {
            CompiledValidationResultIgnoreRule matchingRule = getFirstMatchingRule(result);
            if (matchingRule != null) {
                ignoredCount++;
                logger.debug(
                        "Ignoring validation result via rule {}: expression='{}', message='{}'",
                        StringUtils.defaultIfBlank(matchingRule.id(), "<unnamed>"),
                        StringUtils.defaultString(result.getExpression()),
                        StringUtils.defaultString(result.getMessage()));
                continue;
            }

            filtered.add(result);
        }

        if (ignoredCount > 0) {
            logger.debug("Ignored {} validation result(s) using configured validation-result-ignore rules", ignoredCount);
        }

        return filtered;
    }

    String getFirstMatchingRuleId(Result result) {
        CompiledValidationResultIgnoreRule rule = getFirstMatchingRule(result);
        return rule == null ? null : rule.id();
    }

    private CompiledValidationResultIgnoreRule getFirstMatchingRule(Result result) {
        for (CompiledValidationResultIgnoreRule rule : rules) {
            if (rule.matcher().isMatch(result)) {
                return rule;
            }
        }

        return null;
    }

    private CompiledValidationResultIgnoreRule compileRule(ValidationResultIgnoreRuleConfig config) {
        ValidationResultIgnoreRuleConfig.MatcherConfig matcherConfig = config.getMatcher();
        if (matcherConfig == null) {
            throw new IllegalStateException("Validation-result-ignore rule '" + StringUtils.defaultIfBlank(config.getId(), "<unnamed>") + "' is missing matcher");
        }

        return new CompiledValidationResultIgnoreRule(config.getId(), buildMatcher(matcherConfig));
    }

    private Matcher buildMatcher(ValidationResultIgnoreRuleConfig.MatcherConfig config) {
        if (CollectionUtils.isNotEmpty(config.getChildren())) {
            CompositeMatcher matcher = new CompositeMatcher();
            matcher.setInverted(config.isInverted());
            matcher.setRequiresAllChildren(config.isRequiresAllChildren());
            matcher.setChildren(config.getChildren().stream().map(this::buildMatcher).toList());
            return matcher;
        }

        RegexMatcher matcher = new RegexMatcher();
        matcher.setInverted(config.isInverted());
        matcher.setField(config.getField());
        matcher.setRegex(config.getRegex());
        return matcher;
    }

    private record CompiledValidationResultIgnoreRule(String id, Matcher matcher) {
    }
}