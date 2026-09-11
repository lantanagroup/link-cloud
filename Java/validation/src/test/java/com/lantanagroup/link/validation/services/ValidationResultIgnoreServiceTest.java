package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.configs.ValidationResultIgnoreRuleConfig;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultField;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;

class ValidationResultIgnoreServiceTest {

    @Test
    void filterIgnored_removesResultWhenAnyRuleMatches() {
        LinkConfig config = new LinkConfig();
        config.setValidationResultIgnoreRules(List.of(messageRule("ignore_deprecated", "deprecated")));
        ValidationResultIgnoreService service = new ValidationResultIgnoreService(config);

        Result ignored = new Result();
        ignored.setMessage("The extension http://example is deprecated");

        Result kept = new Result();
        kept.setMessage("A different validation message");

        List<Result> filtered = service.filterIgnored(List.of(ignored, kept));

        assertEquals(1, filtered.size());
        assertEquals(kept, filtered.get(0));
    }

    @Test
    void getFirstMatchingRule_requiresAllConfiguredFieldsToMatch() {
        LinkConfig config = new LinkConfig();
        config.setValidationResultIgnoreRules(List.of(expressionAndMessageRule()));
        ValidationResultIgnoreService service = new ValidationResultIgnoreService(config);

        Result result = new Result();
        result.setExpression("Bundle.entry[0].resource.ofType(MeasureReport).extension[0][url='http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.population.description']");
        result.setMessage("Unknown extension http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.population.description");

        String ruleId = service.getFirstMatchingRuleId(result);

        assertNotNull(ruleId);
        assertEquals("ignore_measure_report_population_description", ruleId);
    }

    @Test
    void getFirstMatchingRule_returnsNullWhenOneRequiredFieldDoesNotMatch() {
        LinkConfig config = new LinkConfig();
        config.setValidationResultIgnoreRules(List.of(expressionAndMessageRule()));
        ValidationResultIgnoreService service = new ValidationResultIgnoreService(config);

        Result result = new Result();
        result.setExpression("Bundle.entry[0].resource.ofType(Patient).extension[0]");
        result.setMessage("Unknown extension http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.population.description");
        result.setSeverity(OperationOutcome.IssueSeverity.INFORMATION);

        assertNull(service.getFirstMatchingRuleId(result));
    }

    private static ValidationResultIgnoreRuleConfig messageRule(String id, String regex) {
        ValidationResultIgnoreRuleConfig rule = new ValidationResultIgnoreRuleConfig();
        rule.setId(id);
        ValidationResultIgnoreRuleConfig.MatcherConfig matcher = new ValidationResultIgnoreRuleConfig.MatcherConfig();
        matcher.setField(ResultField.MESSAGE);
        matcher.setRegex(regex);
        rule.setMatcher(matcher);
        return rule;
    }

    private static ValidationResultIgnoreRuleConfig expressionAndMessageRule() {
        ValidationResultIgnoreRuleConfig rule = new ValidationResultIgnoreRuleConfig();
        rule.setId("ignore_measure_report_population_description");

        ValidationResultIgnoreRuleConfig.MatcherConfig expressionMatcher = new ValidationResultIgnoreRuleConfig.MatcherConfig();
        expressionMatcher.setField(ResultField.EXPRESSION);
        expressionMatcher.setRegex("Bundle\\.entry\\[[0-9]+\\]\\.resource\\.ofType\\(MeasureReport\\)\\.extension\\[[0-9]+\\]\\[url='http://hl7\\.org/fhir/5\\.0/StructureDefinition/extension-MeasureReport\\.population\\.description'\\]");

        ValidationResultIgnoreRuleConfig.MatcherConfig messageMatcher = new ValidationResultIgnoreRuleConfig.MatcherConfig();
        messageMatcher.setField(ResultField.MESSAGE);
        messageMatcher.setRegex("^Unknown extension http://hl7\\.org/fhir/5\\.0/StructureDefinition/extension-MeasureReport\\.population\\.description$");

        ValidationResultIgnoreRuleConfig.MatcherConfig compositeMatcher = new ValidationResultIgnoreRuleConfig.MatcherConfig();
        compositeMatcher.setChildren(List.of(expressionMatcher, messageMatcher));
        compositeMatcher.setRequiresAllChildren(true);
        rule.setMatcher(compositeMatcher);
        return rule;
    }
}