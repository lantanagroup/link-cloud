package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import com.azure.core.util.BinaryData;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.entities.PatientSubmissionModel;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.services.ReportClient;
import com.lantanagroup.link.validation.configs.PreQualificationConfig;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import com.lantanagroup.link.validation.records.ValidationComplete;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.CategoryRuleRepository;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.hl7.fhir.r4.model.Bundle;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.SendResult;

import java.io.InputStream;
import java.util.Collections;
import java.util.List;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class ReadyForValidationConsumerTest {

    private static final String FACILITY_ID = "facility-1";
    private static final String PATIENT_ID = "patient-1";
    private static final String REPORT_ID = "report-1";
    private static final String CORRELATION_ID = "corr-123";
    private static final String PAYLOAD_URI =
            "https://myaccount.blob.core.windows.net/mycontainer/myfile.ndjson";
    private static final String TOPIC = "ready-for-validation";

    @Mock private FhirContext fhirContext;
    @Mock private ReportClient reportClient;
    @Mock private ValidationService validationService;
    @Mock private CategorizationService categorizationService;
    @Mock private ResultRepository resultRepository;
    @Mock private KafkaTemplate<String, ValidationComplete> validationCompleteTemplate;
    @Mock private ValidationMetrics validationMetrics;
    @Mock private BlobStorageService blobStorageService;
    @Mock private ConsumerRecordRecoverer recoverer;

    private ReadyForValidationConsumer consumer;
    private ReadyForValidationConsumer consumerWithoutBlobStorage;

    // Real instances (no deps); the flag defaults to false so the append path is off by default.
    private PreQualificationConfig preQualificationConfig;
    private PreQualOperationOutcomeBuilder preQualOperationOutcomeBuilder;

    private Bundle bundle;

    @BeforeEach
    @SuppressWarnings("unchecked")
    void setUp() throws Exception {
        preQualificationConfig = new PreQualificationConfig();
        preQualOperationOutcomeBuilder = new PreQualOperationOutcomeBuilder();

        consumer = new ReadyForValidationConsumer(
                fhirContext,
                reportClient,
                validationService,
                categorizationService,
                resultRepository,
                validationCompleteTemplate,
                validationMetrics,
                Optional.of(blobStorageService),
                preQualificationConfig,
                preQualOperationOutcomeBuilder,
                recoverer);

        consumerWithoutBlobStorage = new ReadyForValidationConsumer(
                fhirContext,
                reportClient,
                validationService,
                categorizationService,
                resultRepository,
                validationCompleteTemplate,
                validationMetrics,
                Optional.empty(),
                preQualificationConfig,
                preQualOperationOutcomeBuilder,
                recoverer);

        bundle = new Bundle();

        // Default send stub: succeeds synchronously
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        lenient().when(validationCompleteTemplate.send(any(ProducerRecord.class))).thenReturn(future);
        lenient().when(future.get()).thenReturn(null);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> buildRecord(String payloadUri) {
        return buildRecord(payloadUri, false);
    }

    private ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> buildRecord(
            String payloadUri, boolean withCorrelationId) {
        ReadyForValidation.Key key = new ReadyForValidation.Key();
        key.setFacilityId(FACILITY_ID);

        ReadyForValidation value = new ReadyForValidation();
        value.setPatientId(PATIENT_ID);
        value.setReportTrackingId(REPORT_ID);
        value.setPayloadUri(payloadUri);

        ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> record =
                new ConsumerRecord<>(TOPIC, 0, 0L, key, value);

        if (withCorrelationId) {
            record.headers().add(Headers.CORRELATION_ID, Headers.getBytes(CORRELATION_ID));
        }
        return record;
    }

    /** Stubs reportClient to return a PatientSubmissionModel containing the shared bundle. */
    private void stubRestRetrieval() throws Exception {
        PatientSubmissionModel model = new PatientSubmissionModel();
        model.setBundle(bundle);
        when(reportClient.getSubmissionModel(FACILITY_ID, PATIENT_ID, REPORT_ID)).thenReturn(model);
    }

    /** Creates a Result with the given Category already populated (not null). */
    private Result resultWithCategories(List<Category> categories) {
        Result result = new Result();
        result.setCategories(categories);
        return result;
    }

    private Category categoryWithAcceptable(boolean acceptable) {
        Category category = new Category();
        category.setId(acceptable ? "acceptable-cat" : "unacceptable-cat");
        category.setAcceptable(acceptable);
        return category;
    }

    // -------------------------------------------------------------------------
    // Bundle retrieval: Blob Storage
    // -------------------------------------------------------------------------

    @Test
    void process_withPayloadUri_downloadsBundleFromBlobStorage() throws Exception {
        IParser parser = mock(IParser.class);
        when(fhirContext.newNDJsonParser()).thenReturn(parser);
        when(parser.parseResource(eq(Bundle.class), (InputStream) any())).thenReturn(bundle);
        when(blobStorageService.download("myfile.ndjson"))
                .thenReturn(BinaryData.fromBytes(new byte[0]));
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        consumer.process(buildRecord(PAYLOAD_URI));

        verify(blobStorageService).download("myfile.ndjson");
        verify(reportClient, never()).getSubmissionModel(any(), any(), any());
    }

    @Test
    void process_withPayloadUri_parsesDownloadedDataAsNdJson() throws Exception {
        IParser parser = mock(IParser.class);
        when(fhirContext.newNDJsonParser()).thenReturn(parser);
        when(parser.parseResource(eq(Bundle.class), (InputStream) any())).thenReturn(bundle);
        when(blobStorageService.download(anyString()))
                .thenReturn(BinaryData.fromBytes(new byte[0]));
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        consumer.process(buildRecord(PAYLOAD_URI));

        verify(parser).parseResource(eq(Bundle.class), (InputStream) any());
    }

    // -------------------------------------------------------------------------
    // Bundle retrieval: REST fallback
    // -------------------------------------------------------------------------

    @Test
    void process_withNullPayloadUri_fetchesBundleViaRest() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        consumer.process(buildRecord(null));

        verify(reportClient).getSubmissionModel(FACILITY_ID, PATIENT_ID, REPORT_ID);
        verify(blobStorageService, never()).download(anyString());
    }

    @Test
    void process_withPayloadUriButNoBlobService_fetchesBundleViaRest() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        consumerWithoutBlobStorage.process(buildRecord(PAYLOAD_URI));

        verify(reportClient).getSubmissionModel(FACILITY_ID, PATIENT_ID, REPORT_ID);
        verify(blobStorageService, never()).download(anyString());
    }

    @Test
    void process_reportClientThrows_exceptionPropagates() {
        RuntimeException cause = new RuntimeException("REST error");
        when(reportClient.getSubmissionModel(FACILITY_ID, PATIENT_ID, REPORT_ID)).thenThrow(cause);

        RuntimeException thrown = assertThrows(RuntimeException.class,
                () -> consumer.process(buildRecord(null)));

        assertSame(cause, thrown);
    }

    // -------------------------------------------------------------------------
    // Validation and persistence
    // -------------------------------------------------------------------------

    @Test
    void process_callsValidationServiceWithBundle() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        consumer.process(buildRecord(null));

        verify(validationService).validate(bundle);
    }

    @Test
    void process_setsContextFieldsOnEachResult() throws Exception {
        Result result = resultWithCategories(Collections.emptyList());
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(null));

        assertEquals(FACILITY_ID, result.getFacilityId());
        assertEquals(PATIENT_ID, result.getPatientId());
        assertEquals(REPORT_ID, result.getReportId());
    }

    @Test
    void process_callsCategorizationServiceWithResults() throws Exception {
        Result result = resultWithCategories(Collections.emptyList());
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(null));

        verify(categorizationService).categorize(List.of(result));
    }

    @Test
    void process_savesAllResultsToRepository() throws Exception {
        Result result = resultWithCategories(Collections.emptyList());
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(null));

        verify(resultRepository).saveAll(List.of(result));
    }

    @Test
    void process_inactiveCodeResult_isCategorizedAsInactiveCodeAndPersisted() throws Exception {
        // Wire a real CategorizationService backed by the shipped categories.json so this exercises the
        // actual message -> inactive_code category -> saveAll path for an inactive-code finding.
        CategoryRepository categoryRepository = mock(CategoryRepository.class);
        when(categoryRepository.findAll()).thenReturn(CategoryFixtures.loadShippedCategories());
        CategorizationService realCategorizationService = new CategorizationService(
                new ObjectMapper(), categoryRepository, mock(CategoryRuleRepository.class), resultRepository);

        ReadyForValidationConsumer consumerWithRealCategorization = new ReadyForValidationConsumer(
                fhirContext, reportClient, validationService, realCategorizationService, resultRepository,
                validationCompleteTemplate, validationMetrics, Optional.of(blobStorageService),
                preQualificationConfig, preQualOperationOutcomeBuilder, recoverer);

        Result inactiveResult = new Result();
        inactiveResult.setMessage("The concept '423666004' has a status of inactive and its use should be reviewed.");
        inactiveResult.setExpression("Bundle.entry[0].resource.ofType(Encounter).type[0].coding[0]");
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(List.of(inactiveResult));

        consumerWithRealCategorization.process(buildRecord(null));

        assertTrue(
                inactiveResult.getCategories().stream().anyMatch(c -> "missing_active_encounter_type_code".equals(c.getId())),
                "Inactive-code result should be categorized as inactive_code");
        verify(resultRepository).saveAll(List.of(inactiveResult));
    }

    // -------------------------------------------------------------------------
    // ValidationComplete production
    // -------------------------------------------------------------------------

    @Test
    @SuppressWarnings("unchecked")
    void process_allAcceptableCategories_producesValidationCompleteWithValidTrue() throws Exception {
        Result result = resultWithCategories(List.of(categoryWithAcceptable(true)));
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        ArgumentCaptor<ProducerRecord<String, ValidationComplete>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(captor.capture())).thenReturn(future);
        when(future.get()).thenReturn(null);

        consumer.process(buildRecord(null));

        assertTrue(captor.getValue().value().isValid());
    }

    @Test
    @SuppressWarnings("unchecked")
    void process_anyUnacceptableCategory_producesValidationCompleteWithValidFalse() throws Exception {
        Result result = resultWithCategories(List.of(categoryWithAcceptable(false)));
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        ArgumentCaptor<ProducerRecord<String, ValidationComplete>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(captor.capture())).thenReturn(future);
        when(future.get()).thenReturn(null);

        consumer.process(buildRecord(null));

        assertFalse(captor.getValue().value().isValid());
    }

    @Test
    @SuppressWarnings("unchecked")
    void process_noResults_producesValidationCompleteWithValidTrue() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        ArgumentCaptor<ProducerRecord<String, ValidationComplete>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(captor.capture())).thenReturn(future);
        when(future.get()).thenReturn(null);

        consumer.process(buildRecord(null));

        assertTrue(captor.getValue().value().isValid());
    }

    @Test
    @SuppressWarnings("unchecked")
    void process_validationCompleteContainsPatientIdAndReportId() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        ArgumentCaptor<ProducerRecord<String, ValidationComplete>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(captor.capture())).thenReturn(future);
        when(future.get()).thenReturn(null);

        consumer.process(buildRecord(null));

        ValidationComplete vc = captor.getValue().value();
        assertEquals(PATIENT_ID, vc.getPatientId());
        assertEquals(REPORT_ID, vc.getReportTrackingId());
    }

    @Test
    @SuppressWarnings("unchecked")
    void process_validationCompleteKeyIsSetToFacilityId() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        ArgumentCaptor<ProducerRecord<String, ValidationComplete>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(captor.capture())).thenReturn(future);
        when(future.get()).thenReturn(null);

        consumer.process(buildRecord(null));

        assertEquals(FACILITY_ID, captor.getValue().key());
    }

    @Test
    @SuppressWarnings("unchecked")
    void process_kafkaSendThrows_throwsRuntimeExceptionWithMessage() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(any(ProducerRecord.class))).thenReturn(future);
        when(future.get()).thenThrow(new RuntimeException("broker unavailable"));

        RuntimeException thrown = assertThrows(RuntimeException.class,
                () -> consumer.process(buildRecord(null)));

        assertTrue(thrown.getMessage().contains("Failed to produce ValidationComplete message"));
    }

    // -------------------------------------------------------------------------
    // Correlation ID header propagation
    // -------------------------------------------------------------------------

    @Test
    @SuppressWarnings("unchecked")
    void process_withCorrelationId_includesCorrelationIdHeaderInProducedRecord() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        ArgumentCaptor<ProducerRecord<String, ValidationComplete>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(captor.capture())).thenReturn(future);
        when(future.get()).thenReturn(null);

        consumer.process(buildRecord(null, true));

        org.apache.kafka.common.header.Header header =
                captor.getValue().headers().lastHeader(Headers.CORRELATION_ID);
        assertNotNull(header, "Expected correlation ID header to be present");
        assertEquals(CORRELATION_ID, Headers.getString(header.value()));
    }

    @Test
    @SuppressWarnings("unchecked")
    void process_withoutCorrelationId_doesNotIncludeCorrelationIdHeader() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        ArgumentCaptor<ProducerRecord<String, ValidationComplete>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        CompletableFuture<SendResult<String, ValidationComplete>> future = mock(CompletableFuture.class);
        when(validationCompleteTemplate.send(captor.capture())).thenReturn(future);
        when(future.get()).thenReturn(null);

        consumer.process(buildRecord(null, false));

        assertNull(captor.getValue().headers().lastHeader(Headers.CORRELATION_ID));
    }

    // -------------------------------------------------------------------------
    // Metrics
    // -------------------------------------------------------------------------

    @Test
    void process_alwaysRecordsMetrics() throws Exception {
        stubRestRetrieval();
        when(validationService.validate(bundle)).thenReturn(Collections.emptyList());

        consumer.process(buildRecord(null));

        verify(validationMetrics).addToValidationCounter(any());
        verify(validationMetrics).recordValidationDuration(anyDouble(), any());
    }

    // -------------------------------------------------------------------------
    // Pre-qualification OperationOutcome append
    // -------------------------------------------------------------------------

    /** Stubs the blob download path so the bundle is retrieved from ABS (not REST). */
    private void stubBlobDownload() {
        IParser parser = mock(IParser.class);
        when(fhirContext.newNDJsonParser()).thenReturn(parser);
        when(parser.parseResource(eq(Bundle.class), (InputStream) any())).thenReturn(bundle);
        when(blobStorageService.download("myfile.ndjson")).thenReturn(BinaryData.fromBytes(new byte[0]));
    }

    @Test
    void process_flagOn_unacceptableFindings_appendsOperationOutcomeToSameBlob() throws Exception {
        preQualificationConfig.setWritePreQualOperationOutcome(true);
        stubBlobDownload();

        IParser jsonParser = mock(IParser.class);
        when(fhirContext.newJsonParser()).thenReturn(jsonParser);
        when(jsonParser.encodeResourceToString(any()))
                .thenReturn("{\"resourceType\":\"OperationOutcome\"}");

        Result result = resultWithCategories(List.of(categoryWithAcceptable(false)));
        result.setMessage("Code is inactive.");
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(PAYLOAD_URI));

        verify(blobStorageService).appendResource("myfile.ndjson", "{\"resourceType\":\"OperationOutcome\"}");
    }

    @Test
    void process_flagOff_unacceptableFindings_doesNotAppend() throws Exception {
        stubBlobDownload(); // flag defaults to false

        Result result = resultWithCategories(List.of(categoryWithAcceptable(false)));
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(PAYLOAD_URI));

        verify(blobStorageService, never()).appendResource(anyString(), anyString());
    }

    @Test
    void process_flagOn_noUnacceptableFindings_doesNotAppend() throws Exception {
        preQualificationConfig.setWritePreQualOperationOutcome(true);
        stubBlobDownload();

        Result result = resultWithCategories(List.of(categoryWithAcceptable(true)));
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(PAYLOAD_URI));

        verify(blobStorageService, never()).appendResource(anyString(), anyString());
    }

    @Test
    void process_flagOn_bundleAlreadyHasPreQualOperationOutcome_doesNotAppendAgain() throws Exception {
        // Replay: AsyncListener acknowledges the offset even when process() throws, so a failure after the
        // append (producing ValidationComplete, say) routes the record to the retry topic and process()
        // runs again. The re-read bundle then already contains the OperationOutcome we appended, and we
        // must not append a second one.
        preQualificationConfig.setWritePreQualOperationOutcome(true);

        org.hl7.fhir.r4.model.OperationOutcome existing = new org.hl7.fhir.r4.model.OperationOutcome();
        existing.addExtension(new org.hl7.fhir.r4.model.Extension(
                PreQualOperationOutcomeBuilder.OO_TOTAL_URL, new org.hl7.fhir.r4.model.IntegerType(1)));
        bundle.addEntry().setResource(existing);

        stubBlobDownload();

        Result result = resultWithCategories(List.of(categoryWithAcceptable(false)));
        result.setMessage("Code is inactive.");
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(PAYLOAD_URI));

        verify(blobStorageService, never()).appendResource(anyString(), anyString());
    }

    @Test
    void process_flagOn_bundleHasUnrelatedOperationOutcome_stillAppends() throws Exception {
        // The Report service's legacy flat OperationOutcome carries no oo-total extension, so it must not
        // be mistaken for ours and suppress the append.
        preQualificationConfig.setWritePreQualOperationOutcome(true);

        org.hl7.fhir.r4.model.OperationOutcome legacy = new org.hl7.fhir.r4.model.OperationOutcome();
        legacy.addIssue().setDiagnostics("Patient has failed Validation");
        bundle.addEntry().setResource(legacy);

        stubBlobDownload();

        IParser jsonParser = mock(IParser.class);
        when(fhirContext.newJsonParser()).thenReturn(jsonParser);
        when(jsonParser.encodeResourceToString(any())).thenReturn("{\"resourceType\":\"OperationOutcome\"}");

        Result result = resultWithCategories(List.of(categoryWithAcceptable(false)));
        result.setMessage("Code is inactive.");
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(PAYLOAD_URI));

        verify(blobStorageService).appendResource("myfile.ndjson", "{\"resourceType\":\"OperationOutcome\"}");
    }

    @Test
    void process_flagOn_noBlobService_doesNotAppend() throws Exception {
        preQualificationConfig.setWritePreQualOperationOutcome(true);
        stubRestRetrieval(); // no blob service -> bundle comes via REST, append is skipped

        Result result = resultWithCategories(List.of(categoryWithAcceptable(false)));
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumerWithoutBlobStorage.process(buildRecord(PAYLOAD_URI));

        verify(blobStorageService, never()).appendResource(anyString(), anyString());
    }

    @Test
    void process_flagOn_nullPayloadUri_doesNotAppend() throws Exception {
        // Blob storage IS configured here, unlike the test above; only the payloadUri is missing. This
        // covers the second operand of the append guard on its own - drop it and BlobUrlParts.parse(null)
        // throws, so the two operands need separate coverage.
        preQualificationConfig.setWritePreQualOperationOutcome(true);
        stubRestRetrieval(); // no payload URI -> bundle comes via REST

        Result result = resultWithCategories(List.of(categoryWithAcceptable(false)));
        result.setMessage("Code is inactive.");
        when(validationService.validate(bundle)).thenReturn(List.of(result));

        consumer.process(buildRecord(null));

        verify(blobStorageService, never()).appendResource(anyString(), anyString());
    }
}
