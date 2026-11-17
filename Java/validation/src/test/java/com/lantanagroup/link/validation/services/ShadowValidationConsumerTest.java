package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.entities.PatientSubmissionModel;
import com.lantanagroup.link.shared.services.ReportClient;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.EvaluateRequestDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.records.BridgeOutcome;
import com.lantanagroup.link.validation.records.ShadowCompareEvent;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;

import java.util.Collections;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class ShadowValidationConsumerTest {

    private static final String FACILITY_ID = "facility-1";
    private static final String PATIENT_ID = "patient-1";
    private static final String REPORT_ID = "report-1";
    private static final String CORRELATION_ID = "corr-1";
    private static final UUID REQUEST_ID = UUID.randomUUID();
    private static final String RUBRIC_ID = "measure-report-submission-v1";
    private static final String TOPIC = "ShadowCompareEvent";

    @Mock private FhirContext fhirContext;
    @Mock private ReportClient reportClient;
    @Mock private ValidationService validationService;
    @Mock private CategorizationService categorizationService;
    @Mock private RubricExecutionService rubricExecutionService;
    @Mock private LegacyResultMapper legacyResultMapper;
    @Mock private LegacyShadowResultPersister legacyShadowResultPersister;
    @Mock private ShadowComparator shadowComparator;
    @Mock private ConsumerRecordRecoverer recoverer;

    private ObjectMapper objectMapper;
    private ShadowValidationConsumer consumer;
    private Bundle bundle;

    @BeforeEach
    void setUp() {
        objectMapper = new ObjectMapper();
        consumer = new ShadowValidationConsumer(
                fhirContext, reportClient, validationService, categorizationService,
                Optional.empty(), rubricExecutionService, legacyResultMapper, objectMapper,
                legacyShadowResultPersister, shadowComparator, RUBRIC_ID, recoverer);
        bundle = new Bundle();
    }

    private ConsumerRecord<String, ShadowCompareEvent> buildRecord(boolean ranNewEngine, List<ShadowFindingDto> authoritative) {
        ShadowCompareEvent event = new ShadowCompareEvent();
        // Only meaningful when ranNewEngine is true (mirrors production: the rubric engine's own id,
        // present only on that direction); harmless to set unconditionally here since the false-direction
        // test never asserts on it.
        event.setRequestId(REQUEST_ID);
        event.setCorrelationId(CORRELATION_ID);
        event.setFacilityId(FACILITY_ID);
        event.setPatientId(PATIENT_ID);
        event.setReportId(REPORT_ID);
        event.setPayloadUri(null); // forces the REST fallback, avoiding blob-storage mocking
        event.setRanNewEngine(ranNewEngine);
        event.setAuthoritativeResult(authoritative);
        return new ConsumerRecord<>(TOPIC, 0, 0L, FACILITY_ID, event);
    }

    private void stubRestRetrieval() {
        PatientSubmissionModel model = new PatientSubmissionModel();
        model.setBundle(bundle);
        when(reportClient.getSubmissionModel(FACILITY_ID, PATIENT_ID, REPORT_ID)).thenReturn(model);
    }

    private ShadowFindingDto authoritativeFinding() {
        return ShadowFindingDto.builder()
                .severity(OperationOutcome.IssueSeverity.ERROR)
                .code(OperationOutcome.IssueType.INVALID)
                .location("loc")
                .expression("expr")
                .build();
    }

    @Test
    @SuppressWarnings("unchecked")
    void ranNewEngineTrue_runsOnlyLegacyAndPersistsItsAuditTrail() {
        stubRestRetrieval();
        Result legacyResult = new Result();
        legacyResult.setSeverity(OperationOutcome.IssueSeverity.ERROR);
        when(validationService.validate(bundle)).thenReturn(List.of(legacyResult));

        consumer.process(buildRecord(true, List.of(authoritativeFinding())));

        verify(validationService).validate(bundle);
        verify(categorizationService).categorize(List.of(legacyResult));
        verify(rubricExecutionService, never()).evaluate(anyString(), any(), any(), eq(true), any());
        verify(legacyShadowResultPersister).persist(eq(REQUEST_ID), eq(CORRELATION_ID), eq(FACILITY_ID),
                eq(PATIENT_ID), eq(REPORT_ID), eq(List.of(legacyResult)), any(), any());

        ArgumentCaptor<List<Result>> newResultsCaptor = ArgumentCaptor.forClass(List.class);
        verify(shadowComparator).compareAndLog(eq(CORRELATION_ID), eq(REQUEST_ID), eq(FACILITY_ID), eq(PATIENT_ID),
                eq(REPORT_ID), eq(RUBRIC_ID), eq(true), eq(List.of(legacyResult)), newResultsCaptor.capture());
        assertEquals(1, newResultsCaptor.getValue().size());
    }

    @Test
    @SuppressWarnings("unchecked")
    void ranNewEngineFalse_runsOnlyModernEngineAndPersistsRubricResultAsUsual() {
        stubRestRetrieval();
        IParser jsonParser = org.mockito.Mockito.mock(IParser.class);
        when(fhirContext.newJsonParser()).thenReturn(jsonParser);
        when(jsonParser.encodeResourceToString(bundle)).thenReturn("{\"resourceType\":\"Bundle\"}");
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder().build();
        when(rubricExecutionService.evaluate(eq(RUBRIC_ID), isNull(), any(EvaluateRequestDto.class), eq(true), eq(CORRELATION_ID)))
                .thenReturn(envelope);
        when(legacyResultMapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID))
                .thenReturn(new BridgeOutcome(Collections.emptyList(), null));

        consumer.process(buildRecord(false, List.of(authoritativeFinding())));

        verify(rubricExecutionService).evaluate(eq(RUBRIC_ID), isNull(), any(EvaluateRequestDto.class), eq(true), eq(CORRELATION_ID));
        verify(validationService, never()).validate(any());
        verify(legacyShadowResultPersister, never()).persist(any(), any(), any(), any(), any(), any(), any(), any());
        verify(shadowComparator).compareAndLog(eq(CORRELATION_ID), eq(REQUEST_ID), eq(FACILITY_ID), eq(PATIENT_ID),
                eq(REPORT_ID), eq(RUBRIC_ID), eq(false), any(List.class), eq(Collections.emptyList()));
    }

    @Test
    void bundleResolutionThrows_isCaughtAndNeverPropagates() {
        when(reportClient.getSubmissionModel(FACILITY_ID, PATIENT_ID, REPORT_ID))
                .thenThrow(new RuntimeException("REST error"));

        consumer.process(buildRecord(true, List.of()));

        verifyNoInteractions(shadowComparator, legacyShadowResultPersister);
    }

    @Test
    void legacyValidationThrows_isCaughtAndNeverPropagates() {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenThrow(new RuntimeException("validation blew up"));

        consumer.process(buildRecord(true, List.of()));

        verifyNoInteractions(shadowComparator, legacyShadowResultPersister);
    }
}
