package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.context.support.IValidationSupport;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.providers.RemoteTermServiceValidation;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import org.hl7.fhir.common.hapi.validation.support.CommonCodeSystemsTerminologyService;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
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
}
