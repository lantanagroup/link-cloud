package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationResult;
import ca.uhn.fhir.context.support.IValidationSupport.IssueSeverity;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.gclient.IOperation;
import ca.uhn.fhir.rest.gclient.IOperationUnnamed;
import ca.uhn.fhir.rest.gclient.IOperationUntyped;
import ca.uhn.fhir.rest.gclient.IOperationUntypedWithInputAndPartialOutput;
import ca.uhn.fhir.rest.server.exceptions.InvalidRequestException;
import ca.uhn.fhir.rest.server.exceptions.ResourceNotFoundException;
import org.hl7.fhir.r4.model.BooleanType;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.StringType;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.doReturn;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.spy;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

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
}
