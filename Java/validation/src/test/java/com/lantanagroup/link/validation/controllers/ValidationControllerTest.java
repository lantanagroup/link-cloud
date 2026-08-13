package com.lantanagroup.link.validation.controllers;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import com.lantanagroup.link.shared.services.ReportClient;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import com.lantanagroup.link.validation.services.CategorizationService;
import com.lantanagroup.link.validation.services.PreQualService;
import com.lantanagroup.link.validation.services.RubricExecutionService;
import com.lantanagroup.link.validation.services.ValidationService;
import org.hamcrest.Matchers;
import org.hl7.fhir.r4.model.Patient;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

import java.util.List;
import java.util.UUID;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

/**
 * Covers the {@code ApiResponse} envelope introduced on {@link ValidationController} and the
 * {@link GlobalExceptionHandler} error paths that now apply to it (previously framework defaults).
 */
@WebMvcTest(ValidationController.class)
@AutoConfigureMockMvc(addFilters = false)
class ValidationControllerTest {

    private static final String BASE = "/api/validation";

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private ReportClient reportClient;

    @MockBean
    private FhirContext fhirContext;

    @MockBean
    private ValidationService validationService;

    @MockBean
    private CategorizationService categorizationService;

    @MockBean
    private ResultRepository resultRepository;

    @MockBean
    private PreQualService preQualService;

    @MockBean
    private RubricExecutionService rubricExecutionService;

    @Nested
    @DisplayName("POST /$validate")
    class Validate {

        @Test
        @DisplayName("valid resource -> 200, envelope with data.* results")
        void validate_valid() throws Exception {
            IParser parser = mock(IParser.class);
            when(fhirContext.newJsonParser()).thenReturn(parser);
            when(parser.parseResource(anyString())).thenReturn(new Patient());
            when(validationService.validate(any())).thenReturn(List.of(new Result()));

            mockMvc.perform(post(BASE + "/$validate")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content("{\"resourceType\":\"Patient\",\"id\":\"p1\"}"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.message").value("Validation completed"))
                    .andExpect(jsonPath("$.data").isArray());
        }

        @Test
        @DisplayName("blank body -> 400 envelope (ResponseStatusException reshaped)")
        void validate_blankBody() throws Exception {
            // @RequestBody String is read by StringHttpMessageConverter (raw bytes, no JSON decoding), so a
            // single space is a present-but-blank body; a zero-byte body instead trips Spring's
            // "required request body is missing" check (HttpMessageNotReadableException).
            mockMvc.perform(post(BASE + "/$validate")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content(" "))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.status").value(400))
                    .andExpect(jsonPath("$.message").value("No resource provided"))
                    .andExpect(jsonPath("$.timestamp").exists())
                    .andExpect(jsonPath("$.data").doesNotExist());
        }
    }

    @Nested
    @DisplayName("POST /v2/rubrics/{rubricId}/$evaluate")
    class Evaluate {

        @Test
        @DisplayName("valid request -> 200, envelope wraps the Evaluation envelope under data")
        void evaluate_valid() throws Exception {
            UUID requestId = UUID.randomUUID();
            ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                    .requestId(requestId)
                    .rubricId("piqi.core")
                    .rubricVersion("1.0.0")
                    .status(RubricResultStatus.ACCEPTABLE)
                    .build();
            when(rubricExecutionService.evaluate(eq("piqi.core"), isNull(), any(), eq(true)))
                    .thenReturn(envelope);

            mockMvc.perform(post(BASE + "/v2/rubrics/piqi.core/$evaluate")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content("{\"payload\": {\"resourceType\": \"Patient\"}}"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.message").value("Evaluation completed"))
                    .andExpect(jsonPath("$.data.requestId").value(requestId.toString()))
                    .andExpect(jsonPath("$.data.rubricId").value("piqi.core"));
        }

        @Test
        @DisplayName("missing payload -> 400 envelope with errors[]")
        void evaluate_missingPayload() throws Exception {
            mockMvc.perform(post(BASE + "/v2/rubrics/piqi.core/$evaluate")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content("{}"))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.status").value(400))
                    .andExpect(jsonPath("$.message").value("Request validation failed"))
                    .andExpect(jsonPath("$.errors[0]").value("payload: must not be null"));
        }
    }

    @Nested
    @DisplayName("POST /v2/rubrics/{rubricId}/versions/{semver}/$dry-run")
    class DryRun {

        @Test
        @DisplayName("valid request -> 200, envelope with message 'Dry-run completed'")
        void dryRun_valid() throws Exception {
            ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                    .requestId(UUID.randomUUID())
                    .rubricId("piqi.core")
                    .rubricVersion("1.0.0")
                    .status(RubricResultStatus.ACCEPTABLE)
                    .build();
            when(rubricExecutionService.evaluate(eq("piqi.core"), eq("1.0.0"), any(), eq(false)))
                    .thenReturn(envelope);

            mockMvc.perform(post(BASE + "/v2/rubrics/piqi.core/versions/1.0.0/$dry-run")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content("{\"payload\": {\"resourceType\": \"Patient\"}}"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.message").value("Dry-run completed"))
                    .andExpect(jsonPath("$.data.rubricVersion").value("1.0.0"));
        }
    }

    @Nested
    @DisplayName("Exception handler paths new to ValidationController")
    class ExceptionHandling {

        @Test
        @DisplayName("bad enum query param on pre-qual severity -> 400 envelope naming the parameter")
        void preQual_badSeverityEnum() throws Exception {
            mockMvc.perform(get(BASE + "/pre-qual/facility-1/report-1")
                            .param("severity", "NOPE"))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.status").value(400))
                    .andExpect(jsonPath("$.message").value(Matchers.containsString("severity")));
        }

        @Test
        @DisplayName("malformed JSON body on $categorize -> 400 envelope 'Malformed request body'")
        void categorize_malformedBody() throws Exception {
            mockMvc.perform(post(BASE + "/$categorize")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content("{ not json"))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.message").value("Malformed request body"));
        }

        @Test
        @DisplayName("uncaught exception -> 500 envelope 'An unexpected error occurred'")
        void evaluate_uncaughtException() throws Exception {
            when(rubricExecutionService.evaluate(eq("piqi.core"), isNull(), any(), eq(true)))
                    .thenThrow(new RuntimeException("boom"));

            mockMvc.perform(post(BASE + "/v2/rubrics/piqi.core/$evaluate")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content("{\"payload\": {\"resourceType\": \"Patient\"}}"))
                    .andExpect(status().isInternalServerError())
                    .andExpect(jsonPath("$.status").value(500))
                    .andExpect(jsonPath("$.message").value("An unexpected error occurred"))
                    .andExpect(jsonPath("$.data").doesNotExist())
                    .andExpect(jsonPath("$.errors").doesNotExist());
        }
    }
}
