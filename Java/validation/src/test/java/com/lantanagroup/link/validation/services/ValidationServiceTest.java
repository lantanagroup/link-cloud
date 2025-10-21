package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.context.support.IValidationSupport;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.configs.ValidationResultIgnoreRuleConfig;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultField;
import com.lantanagroup.link.validation.providers.RemoteTermServiceValidation;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import org.hl7.fhir.common.hapi.validation.support.CommonCodeSystemsTerminologyService;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Verifies that the terminology support chain always includes the in-memory fallback
 * (CommonCodeSystems + InMemory) even when a remote terminology service is configured,
 * and that the remote support is consulted before the in-memory fallback. See LEGLINK-601.
 */
class ValidationServiceTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    private static ValidationSupportChain newChain() {
        return new ValidationSupportChain(new DefaultProfileValidationSupport(FHIR_CONTEXT));
    }

    private static LinkConfig config(String fhirTerminologyUrl, String linkTerminologyUrl) {
        LinkConfig config = mock(LinkConfig.class);
        when(config.getFhirTerminologyServiceUrl()).thenReturn(fhirTerminologyUrl);
        when(config.getTerminologyServiceUrl()).thenReturn(linkTerminologyUrl);
        when(config.getWhiteListCodeSystemRegex()).thenReturn(new ArrayList<>());
        when(config.getWhiteListValueSetRegex()).thenReturn(new ArrayList<>());
        return config;
    }

    private static int indexOfSupport(ValidationSupportChain chain, Class<?> type) {
        List<IValidationSupport> supports = chain.getValidationSupports();
        for (int i = 0; i < supports.size(); i++) {
            if (type.isInstance(supports.get(i))) {
                return i;
            }
        }
        return -1;
    }

    private static boolean hasSupport(ValidationSupportChain chain, Class<?> type) {
        return indexOfSupport(chain, type) >= 0;
    }

    @Test
    void linkTerminologyService_registersRemoteWithInMemoryFallbackAfterIt() {
        ValidationSupportChain chain = newChain();

        ValidationService.loadTerminologyValidationSupport(
                FHIR_CONTEXT, config(null, "http://link-terminology:8076"), chain, mock(ValidationCacheService.class));

        assertTrue(hasSupport(chain, RemoteTermServiceValidation.class), "remote support present");
        assertTrue(hasSupport(chain, CommonCodeSystemsTerminologyService.class), "CommonCodeSystems fallback present");
        assertTrue(hasSupport(chain, InMemoryTerminologyServerValidationSupport.class), "InMemory fallback present");
        assertTrue(
                indexOfSupport(chain, RemoteTermServiceValidation.class)
                        < indexOfSupport(chain, InMemoryTerminologyServerValidationSupport.class),
                "remote support must be consulted before the in-memory fallback");
    }

    @Test
    void fhirTerminologyService_registersRemoteWithInMemoryFallback() {
        ValidationSupportChain chain = newChain();

        ValidationService.loadTerminologyValidationSupport(
                FHIR_CONTEXT, config("http://fhir-terminology/fhir", null), chain, mock(ValidationCacheService.class));

        assertTrue(hasSupport(chain, RemoteTermServiceValidation.class), "remote support present");
        assertTrue(hasSupport(chain, CommonCodeSystemsTerminologyService.class), "CommonCodeSystems fallback present");
        assertTrue(hasSupport(chain, InMemoryTerminologyServerValidationSupport.class), "InMemory fallback present");
    }

    @Test
    void noRemoteTerminologyService_registersInMemoryWithoutRemote() {
        ValidationSupportChain chain = newChain();

        ValidationService.loadTerminologyValidationSupport(
                FHIR_CONTEXT, config(null, null), chain, mock(ValidationCacheService.class));

        assertFalse(hasSupport(chain, RemoteTermServiceValidation.class), "no remote support when none configured");
        assertTrue(hasSupport(chain, CommonCodeSystemsTerminologyService.class), "CommonCodeSystems present");
        assertTrue(hasSupport(chain, InMemoryTerminologyServerValidationSupport.class), "InMemory present");
    }

    @Test
    void deduplicateInactiveResults_collapsesDuplicateInactiveWarningsPerElement() {
        // HAPI validates a bound coding against both its code system and its value set, so the same inactive
        // code surfaces twice for one element -- differing only by a trailing "(for 'system#code')" suffix.
        Result inactiveWithSuffix = result(
                "Bundle.entry[1].resource.ofType(Patient).extension[0].extension[1].value.ofType(Coding)", "1:398",
                "The concept '1004-1' has a status of inactive and its use should be reviewed. (for 'urn:oid:2.16.840.1.113883.6.238#1004-1')");
        Result inactiveNoSuffix = result(
                "Bundle.entry[1].resource.ofType(Patient).extension[0].extension[1].value.ofType(Coding)", "1:398",
                "The concept '1004-1' has a status of inactive and its use should be reviewed.");
        Result otherFinding = result(
                "Bundle.entry[0].resource.ofType(Encounter).type[0]", "1:408",
                "None of the codings provided are in the value set 'US Core Encounter Type'");
        Result inactiveDifferentElement = result(
                "Bundle.entry[0].resource.ofType(Encounter).class", "1:200",
                "The concept 'SS' has a status of inactive and its use should be reviewed.");

        List<Result> deduplicated = ValidationService.deduplicateInactiveResults(
                List.of(inactiveWithSuffix, inactiveNoSuffix, otherFinding, inactiveDifferentElement));

        // The two variants of the 1004-1 warning collapse to the first; unrelated findings are untouched.
        assertEquals(3, deduplicated.size());
        assertTrue(deduplicated.contains(inactiveWithSuffix), "first inactive variant kept");
        assertFalse(deduplicated.contains(inactiveNoSuffix), "duplicate inactive variant dropped");
        assertTrue(deduplicated.contains(otherFinding), "non-inactive finding preserved");
        assertTrue(deduplicated.contains(inactiveDifferentElement), "inactive on a different element preserved");
    }

    @Test
    void validationResultIgnoreService_doesNotMatchWhenNoRulesConfigured() {
        LinkConfig config = mock(LinkConfig.class);
        when(config.getValidationResultIgnoreRules()).thenReturn(null);

        ValidationResultIgnoreService service = new ValidationResultIgnoreService(config);

        assertNull(service.getFirstMatchingRuleId(result("expr", "1:1", "message")));
    }

    @Test
    void validationResultIgnoreService_matchesConfiguredMessageRule() {
        ValidationResultIgnoreRuleConfig rule = new ValidationResultIgnoreRuleConfig();
        rule.setId("ignore_deprecated");
        ValidationResultIgnoreRuleConfig.MatcherConfig matcher = new ValidationResultIgnoreRuleConfig.MatcherConfig();
        matcher.setField(ResultField.MESSAGE);
        matcher.setRegex("deprecated");
        rule.setMatcher(matcher);

        LinkConfig config = mock(LinkConfig.class);
        when(config.getValidationResultIgnoreRules()).thenReturn(List.of(rule));

        ValidationResultIgnoreService service = new ValidationResultIgnoreService(config);

        Result result = result("expr", "1:1", "This extension is deprecated");
        assertEquals("ignore_deprecated", service.getFirstMatchingRuleId(result));
    }

    private static Result result(String expression, String location, String message) {
        Result result = new Result();
        result.setExpression(expression);
        result.setLocation(location);
        result.setMessage(message);
        return result;
    }
}
