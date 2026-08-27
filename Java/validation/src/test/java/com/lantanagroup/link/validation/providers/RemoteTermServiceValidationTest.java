package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationResult;
import ca.uhn.fhir.context.support.IValidationSupport.IssueSeverity;
import ca.uhn.fhir.rest.api.SummaryEnum;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.gclient.ICriterion;
import ca.uhn.fhir.rest.gclient.IOperation;
import ca.uhn.fhir.rest.gclient.IOperationUnnamed;
import ca.uhn.fhir.rest.gclient.IOperationUntyped;
import ca.uhn.fhir.rest.gclient.IOperationUntypedWithInputAndPartialOutput;
import ca.uhn.fhir.rest.gclient.IQuery;
import ca.uhn.fhir.rest.gclient.IUntypedQuery;
import ca.uhn.fhir.rest.server.exceptions.InvalidRequestException;
import ca.uhn.fhir.rest.server.exceptions.ResourceNotFoundException;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.hl7.fhir.instance.model.api.IBaseBundle;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.mockito.MockedStatic;

import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.*;

/**
 * Unit tests for {@link RemoteTermServiceValidation#invokeRemoteValidateCode}. The HAPI generic client
 * chain is mocked (via a stubbed {@code provideClient()}) so the tests exercise the request-building and
 * response-parsing logic without a live terminology server.
 */
class RemoteTermServiceValidationTest {
    private static final String CODE_SYSTEM_URL = "http://loinc.org";
    private static final String VALUE_SET_URL = "http://example.org/ValueSet/vs";
    private static final String CODE = "1234-5";

    private final FhirContext fhirContext = FhirContext.forR4();

    private RemoteTermServiceValidation newSpy() {
        return spy(new RemoteTermServiceValidation(
                null, fhirContext, "http://tx.example.org/fhir", List.of(), List.of()));
    }

    /**
     * Stubs the fluent {@code client.operation().onType(..).named(..).withParameters(..).execute()} chain so
     * that {@code execute()} returns the supplied output. Returns the {@code onType} mock so callers can
     * capture the resource-type argument.
     */
    @SuppressWarnings({"rawtypes", "unchecked"})
    private IOperation stubClientChain(RemoteTermServiceValidation subject, Object executeResult, boolean throwResult) {
        IGenericClient client = mock(IGenericClient.class);
        IOperation operation = mock(IOperation.class);
        IOperationUnnamed unnamed = mock(IOperationUnnamed.class);
        IOperationUntyped untyped = mock(IOperationUntyped.class);
        IOperationUntypedWithInputAndPartialOutput withInput = mock(IOperationUntypedWithInputAndPartialOutput.class);

        doReturn(client).when(subject).provideClient();
        when(client.operation()).thenReturn(operation);
        when(operation.onType(anyString())).thenReturn(unnamed);
        when(unnamed.named(anyString())).thenReturn(untyped);
        when(untyped.withParameters(any())).thenReturn(withInput);
        if (throwResult) {
            when(withInput.execute()).thenThrow((Throwable) executeResult);
        } else {
            when(withInput.execute()).thenReturn(executeResult);
        }
        return operation;
    }

    /**
     * Stubs the fluent {@code client.search().forResource(..).where(..).summaryMode(..).returnBundle(..).execute()}
     * chain so that {@code execute()} returns the supplied bundle. Returns the query mock so callers can capture
     * the summary mode requested.
     */
    @SuppressWarnings({"rawtypes", "unchecked"})
    private IQuery stubSearchChain(RemoteTermServiceValidation subject, IBaseBundle executeResult) {
        IGenericClient client = mock(IGenericClient.class);
        IUntypedQuery untypedQuery = mock(IUntypedQuery.class);
        IQuery query = mock(IQuery.class);

        doReturn(client).when(subject).provideClient();
        when(client.search()).thenReturn(untypedQuery);
        when(untypedQuery.forResource(anyString())).thenReturn(query);
        when(query.where(any(ICriterion.class))).thenReturn(query);
        when(query.summaryMode(any(SummaryEnum.class))).thenReturn(query);
        when(query.returnBundle(any(Class.class))).thenReturn(query);
        when(query.execute()).thenReturn(executeResult);
        return query;
    }

    private Bundle searchsetContaining(Resource resource) {
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.SEARCHSET);
        bundle.addEntry().setResource(resource);
        return bundle;
    }

    private Parameters validateCodeResponse(boolean result, String paramName, String paramValue) {
        Parameters params = new Parameters();
        params.addParameter().setName("result").setValue(new BooleanType(result));
        if (paramName != null) {
            params.addParameter().setName(paramName).setValue(new StringType(paramValue));
        }
        return params;
    }

    @Test
    void invokeRemoteValidateCode_validCode_returnsInformationResultWithDisplay() {
        RemoteTermServiceValidation subject = newSpy();
        stubClientChain(subject, validateCodeResponse(true, "display", "Glucose"), false);

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, "Glucose", null, null);

        assertNotNull(result);
        assertEquals(IssueSeverity.INFORMATION, result.getSeverity());
        assertEquals(CODE, result.getCode());
        assertEquals("Glucose", result.getDisplay());
    }

    @Test
    void invokeRemoteValidateCode_invalidCode_returnsErrorResultWithMessage() {
        RemoteTermServiceValidation subject = newSpy();
        stubClientChain(subject, validateCodeResponse(false, "message", "Unknown code"), false);

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

        assertNotNull(result);
        assertEquals(IssueSeverity.ERROR, result.getSeverity());
        assertEquals("Unknown code", result.getMessage());
    }

    @Test
    void invokeRemoteValidateCode_blankResultParameter_returnsNull() {
        RemoteTermServiceValidation subject = newSpy();
        // "result" present but blank -> the value branch is skipped and null is returned
        Parameters response = new Parameters();
        response.addParameter().setName("result").setValue(new StringType(""));
        stubClientChain(subject, response, false);

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

        assertNull(result);
    }

    @Test
    void invokeRemoteValidateCode_blankCode_returnsNullWithoutCallingServer() {
        RemoteTermServiceValidation subject = newSpy();

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, "  ", null, null, null);

        assertNull(result);
        verify(subject, never()).provideClient();
    }

    @Test
    void invokeRemoteValidateCode_invalidRequestException_returnsErrorResult() {
        RemoteTermServiceValidation subject = newSpy();
        stubClientChain(subject, new InvalidRequestException("bad request"), true);

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

        assertNotNull(result);
        assertEquals(IssueSeverity.ERROR, result.getSeverity());
        assertNotNull(result.getMessage());
    }

    @Test
    void invokeRemoteValidateCode_resourceNotFoundException_returnsErrorResult() {
        RemoteTermServiceValidation subject = newSpy();
        stubClientChain(subject, new ResourceNotFoundException("not found"), true);

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

        assertNotNull(result);
        assertEquals(IssueSeverity.ERROR, result.getSeverity());
        assertNotNull(result.getMessage());
    }

    @Test
    void invokeRemoteValidateCode_noValueSet_operatesOnCodeSystem() {
        RemoteTermServiceValidation subject = newSpy();
        IOperation operation = stubClientChain(subject, validateCodeResponse(true, "display", "Glucose"), false);

        subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, "Glucose", null, null);

        ArgumentCaptor<String> resourceType = ArgumentCaptor.forClass(String.class);
        verify(operation).onType(resourceType.capture());
        assertEquals("CodeSystem", resourceType.getValue());
    }

    @Test
    void invokeRemoteValidateCode_withValueSetUrl_operatesOnValueSet() {
        RemoteTermServiceValidation subject = newSpy();
        IOperation operation = stubClientChain(subject, validateCodeResponse(true, "display", "Glucose"), false);

        subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, "Glucose", VALUE_SET_URL, null);

        ArgumentCaptor<String> resourceType = ArgumentCaptor.forClass(String.class);
        verify(operation).onType(resourceType.capture());
        assertEquals("ValueSet", resourceType.getValue());
    }

    @Test
    void invokeRemoteValidateCode_inactiveCode_returnsWarningResult() {
        RemoteTermServiceValidation subject = newSpy();

        // Response the terminology service returns for a valid-but-inactive code:
        // result=true plus an "issues" OperationOutcome carrying a warning.
        Parameters response = new Parameters();
        response.addParameter().setName("result").setValue(new BooleanType(true));
        OperationOutcome outcome = new OperationOutcome();
        outcome.addIssue()
                .setSeverity(OperationOutcome.IssueSeverity.WARNING)
                .setCode(OperationOutcome.IssueType.BUSINESSRULE)
                .setDetails(new CodeableConcept().setText("Code is inactive."));
        response.addParameter().setName("issues").setResource(outcome);
        stubClientChain(subject, response, false);

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

        assertNotNull(result);
        assertEquals(CODE, result.getCode());
        assertEquals(IssueSeverity.WARNING, result.getSeverity());
        assertEquals(
                "The concept '" + CODE + "' has a status of inactive and its use should be reviewed.",
                result.getMessage());
    }

    @Test
    void invokeRemoteValidateCode_activeCode_returnsInformationResult() {
        RemoteTermServiceValidation subject = newSpy();

        // Response the terminology service returns for a valid code:
        Parameters response = new Parameters();
        response.addParameter().setName("result").setValue(new BooleanType(true));
        stubClientChain(subject, response, false);

        CodeValidationResult result = subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

        assertNotNull(result);
        assertEquals(CODE, result.getCode());
        assertEquals(IssueSeverity.INFORMATION, result.getSeverity());
    }

    @Test
    void invokeRemoteValidateCode_nonInactiveWarning_returnsInformationResult() {
        RemoteTermServiceValidation subject = newSpy();

        // A valid code carrying a business-rule warning whose text is NOT the inactive marker
        // must stay INFORMATION -- only "Code is inactive." escalates to a WARNING.
        Parameters response = new Parameters();
        response.addParameter().setName("result").setValue(new BooleanType(true));
        OperationOutcome outcome = new OperationOutcome();
        outcome.addIssue()
                .setSeverity(OperationOutcome.IssueSeverity.WARNING)
                .setCode(OperationOutcome.IssueType.BUSINESSRULE)
                .setDetails(new CodeableConcept().setText("Some other rule"));
        response.addParameter().setName("issues").setResource(outcome);
        stubClientChain(subject, response, false);

        CodeValidationResult result =
                subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

        assertNotNull(result);
        assertEquals(CODE, result.getCode());
        assertEquals(IssueSeverity.INFORMATION, result.getSeverity());
    }

    @Test
    void validateCodeInValueSet_withCanonicalUrl_delegatesToRemoteViaCache() {
        // HAPI passes the resolved ValueSet resource; when it carries a canonical URL we route through the
        // same cache as validateCode, using the extracted URL (not the inline resource).
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = spy(new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir", List.of(), List.of()));

        CodeValidationResult expected = new CodeValidationResult();
        expected.setCode(CODE);
        expected.setSeverity(IssueSeverity.WARNING);
        when(cacheService.cachedValidateCode(subject, CODE_SYSTEM_URL, CODE, null, VALUE_SET_URL))
                .thenReturn(expected);

        ValueSet valueSet = new ValueSet();
        valueSet.setUrl(VALUE_SET_URL);

        CodeValidationResult result =
                subject.validateCodeInValueSet(null, null, CODE_SYSTEM_URL, CODE, null, valueSet);

        assertSame(expected, result);
        verify(cacheService).cachedValidateCode(subject, CODE_SYSTEM_URL, CODE, null, VALUE_SET_URL);
    }

    @Test
    void validateCodeInValueSet_withoutCanonicalUrl_sendsValueSetInlineAndDetectsInactive() {
        // A ValueSet with no canonical URL is sent to the terminology server inline (resourceType "ValueSet"),
        // and the inactive-code detection still applies to the value-set-bound code.
        RemoteTermServiceValidation subject = newSpy();

        Parameters response = new Parameters();
        response.addParameter().setName("result").setValue(new BooleanType(true));
        OperationOutcome outcome = new OperationOutcome();
        outcome.addIssue()
                .setSeverity(OperationOutcome.IssueSeverity.WARNING)
                .setCode(OperationOutcome.IssueType.BUSINESSRULE)
                .setDetails(new CodeableConcept().setText("Code is inactive."));
        response.addParameter().setName("issues").setResource(outcome);
        IOperation operation = stubClientChain(subject, response, false);

        ValueSet valueSet = new ValueSet(); // no url -> inline

        CodeValidationResult result =
                subject.validateCodeInValueSet(null, null, CODE_SYSTEM_URL, CODE, null, valueSet);

        assertNotNull(result);
        assertEquals(IssueSeverity.WARNING, result.getSeverity());
        assertEquals(
                "The concept '" + CODE + "' has a status of inactive and its use should be reviewed.",
                result.getMessage());
        // The inline path targets the ValueSet resource, not CodeSystem.
        verify(operation).onType("ValueSet");
    }

    // ---------- fetchValueSet summary mode ----------
    // fetchValueSet(String) is the public IValidationSupport hook HAPI calls to resolve a bound ValueSet.
    // Requesting _summary=false makes the Link terminology service enumerate every code of the value set
    // into ValueSet.expansion.contains, which is the client-side trigger for terminology-service memory
    // exhaustion -- and the expansion is never read (validateCodeInValueSet uses only the canonical URL).
    // These tests pin the summary mode so it cannot be silently changed back.

    @Test
    @SuppressWarnings("rawtypes")
    void fetchValueSet_requestsSummaryModeTrue() {
        RemoteTermServiceValidation subject = newSpy();
        ValueSet valueSet = new ValueSet();
        valueSet.setUrl(VALUE_SET_URL);
        IQuery query = stubSearchChain(subject, searchsetContaining(valueSet));

        IBaseResource result = subject.fetchValueSet(VALUE_SET_URL);

        ArgumentCaptor<SummaryEnum> summaryMode = ArgumentCaptor.forClass(SummaryEnum.class);
        verify(query).summaryMode(summaryMode.capture());
        assertEquals(SummaryEnum.TRUE, summaryMode.getValue(),
                "fetchValueSet must not request _summary=false: that makes the terminology service "
                        + "enumerate the full expansion for a resource whose expansion is never read.");
        assertSame(valueSet, result);
    }

    @Test
    @SuppressWarnings("rawtypes")
    void fetchValueSet_noMatch_returnsNull() {
        RemoteTermServiceValidation subject = newSpy();
        Bundle empty = new Bundle();
        empty.setType(Bundle.BundleType.SEARCHSET);
        IQuery query = stubSearchChain(subject, empty);

        assertNull(subject.fetchValueSet(VALUE_SET_URL));
        verify(query).summaryMode(SummaryEnum.TRUE);
    }

    @Test
    @SuppressWarnings("rawtypes")
    void invokeIsValueSetSupported_requestsSummaryModeTrue() {
        // The existence check has always used _summary=true; fetchValueSet(String) now matches it.
        RemoteTermServiceValidation subject = newSpy();
        ValueSet valueSet = new ValueSet();
        valueSet.setUrl(VALUE_SET_URL);
        IQuery query = stubSearchChain(subject, searchsetContaining(valueSet));

        assertTrue(subject.invokeIsValueSetSupported(VALUE_SET_URL));
        verify(query).summaryMode(SummaryEnum.TRUE);
    }

    // ---------- isCodeSystemSupported / isValueSetSupported delegation to cache ----------
    // These methods are hit per-system per-chain-traversal by HAPI's ValidationSupportChain, so the
    // remote lookup has to go through ValidationCacheService. The tests below verify the delegation
    // and confirm the fast pre-cache checks (null input, whitelist regex) short-circuit before the
    // cache is consulted at all.

    @Test
    void isCodeSystemSupported_delegatesToCacheService() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir", List.of(), List.of());
        when(cacheService.cachedIsCodeSystemSupported(subject, CODE_SYSTEM_URL)).thenReturn(true);

        assertTrue(subject.isCodeSystemSupported(null, CODE_SYSTEM_URL));
        verify(cacheService).cachedIsCodeSystemSupported(subject, CODE_SYSTEM_URL);
    }

    @Test
    void isCodeSystemSupported_nullSystem_returnsFalseWithoutTouchingCache() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir", List.of(), List.of());

        assertFalse(subject.isCodeSystemSupported(null, null));
        verifyNoInteractions(cacheService);
    }

    @Test
    void isCodeSystemSupported_matchesWhitelist_returnsFalseWithoutTouchingCache() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir",
                List.of("^http://open\\.epic\\.com/.*"), List.of());

        assertFalse(subject.isCodeSystemSupported(null, "http://open.epic.com/FHIR/foo"));
        verifyNoInteractions(cacheService);
    }

    @Test
    void isValueSetSupported_delegatesToCacheService() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir", List.of(), List.of());
        when(cacheService.cachedIsValueSetSupported(subject, VALUE_SET_URL)).thenReturn(true);

        assertTrue(subject.isValueSetSupported(null, VALUE_SET_URL));
        verify(cacheService).cachedIsValueSetSupported(subject, VALUE_SET_URL);
    }

    @Test
    void isValueSetSupported_nullUrl_returnsFalseWithoutTouchingCache() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir", List.of(), List.of());

        assertFalse(subject.isValueSetSupported(null, null));
        verifyNoInteractions(cacheService);
    }

    @Test
    void isValueSetSupported_matchesWhitelist_returnsFalseWithoutTouchingCache() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir",
                List.of(), List.of("^http://example\\.org/ValueSet/.*"));

        assertFalse(subject.isValueSetSupported(null, "http://example.org/ValueSet/vs"));
        verifyNoInteractions(cacheService);
    }

    @Test
    void fetchValueSet_matchesWhitelist_returnsNullWithoutTouchingCacheOrClient() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = spy(new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir",
                List.of(), List.of("^http://example\\.org/ValueSet/.*")));

        assertNull(subject.fetchValueSet("http://example.org/ValueSet/vs"));
        verifyNoInteractions(cacheService);
        verify(subject, never()).provideClient();
    }

    @Test
    void fetchCodeSystem_matchesWhitelist_returnsNullWithoutTouchingCacheOrClient() {
        ValidationCacheService cacheService = mock(ValidationCacheService.class);
        RemoteTermServiceValidation subject = spy(new RemoteTermServiceValidation(
                cacheService, fhirContext, "http://tx.example.org/fhir",
                List.of("^http://open\\.epic\\.com/.*"), List.of()));

        assertNull(subject.fetchCodeSystem("http://open.epic.com/FHIR/foo"));
        verifyNoInteractions(cacheService);
        verify(subject, never()).provideClient();
    }

    // ---------- log-sanitization tests ----------

    @Test
    void invokeRemoteValidateCode_codeSystemBranch_sanitizesLogArguments() {
        try (MockedStatic<LogUtils> logUtils = mockStatic(LogUtils.class, org.mockito.Answers.CALLS_REAL_METHODS)) {
            RemoteTermServiceValidation subject = newSpy();
            stubClientChain(subject, validateCodeResponse(true, "display", "Glucose"), false);

            subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, null, null);

            logUtils.verify(() -> LogUtils.sanitize(CODE_SYSTEM_URL));
            logUtils.verify(() -> LogUtils.sanitize(CODE));
        }
    }

    @Test
    void invokeRemoteValidateCode_valueSetBranch_sanitizesLogArguments() {
        try (MockedStatic<LogUtils> logUtils = mockStatic(LogUtils.class, org.mockito.Answers.CALLS_REAL_METHODS)) {
            RemoteTermServiceValidation subject = newSpy();
            stubClientChain(subject, validateCodeResponse(true, "display", "Glucose"), false);

            subject.invokeRemoteValidateCode(CODE_SYSTEM_URL, CODE, null, VALUE_SET_URL, null);

            logUtils.verify(() -> LogUtils.sanitize(VALUE_SET_URL));
            logUtils.verify(() -> LogUtils.sanitize(CODE));
        }
    }

    @Test
    @SuppressWarnings({"rawtypes", "unchecked"})
    void invokeLookupCode_catchBlock_sanitizesLogArguments() {
        RemoteTermServiceValidation subject = newSpy();

        IGenericClient client = mock(IGenericClient.class);
        IOperation operation = mock(IOperation.class);
        IOperationUnnamed unnamed = mock(IOperationUnnamed.class);
        IOperationUntyped untyped = mock(IOperationUntyped.class);
        IOperationUntypedWithInputAndPartialOutput withInput = mock(IOperationUntypedWithInputAndPartialOutput.class);

        doReturn(client).when(subject).provideClient();
        when(client.getFhirContext()).thenReturn(fhirContext);
        when(client.operation()).thenReturn(operation);
        when(operation.onType(any(Class.class))).thenReturn(unnamed);
        when(unnamed.named(anyString())).thenReturn(untyped);
        when(untyped.withParameters(any())).thenReturn(withInput);
        when(withInput.useHttpGet()).thenReturn(withInput);
        InvalidRequestException exception = new InvalidRequestException("bad request\nInjected");
        when(withInput.execute()).thenThrow(exception);

        try (MockedStatic<LogUtils> logUtils = mockStatic(LogUtils.class, org.mockito.Answers.CALLS_REAL_METHODS)) {
            subject.invokeLookupCode(CODE, CODE_SYSTEM_URL, null, "");

            // CODE and CODE_SYSTEM_URL each appear in both the happy-path debug log (line 107)
            // and the catch-block debug log (line 131), hence times(2).
            logUtils.verify(() -> LogUtils.sanitize(CODE), org.mockito.Mockito.times(2));
            logUtils.verify(() -> LogUtils.sanitize(CODE_SYSTEM_URL), org.mockito.Mockito.times(2));
            logUtils.verify(() -> LogUtils.sanitize(exception.getMessage()));
        }
    }
}
