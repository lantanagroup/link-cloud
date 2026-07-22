package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobUrlParts;
import com.lantanagroup.link.shared.Timer;
import com.lantanagroup.link.shared.entities.PatientSubmissionModel;
import com.lantanagroup.link.shared.kafka.AsyncListener;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.services.ReportClient;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.configs.PreQualificationConfig;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import com.lantanagroup.link.validation.records.ValidationComplete;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import io.opentelemetry.api.common.Attributes;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.stereotype.Service;

import java.util.List;
import java.util.Optional;

@Service
public class ReadyForValidationConsumer extends AsyncListener<ReadyForValidation.Key, ReadyForValidation> {
    private final Logger _logger = LoggerFactory.getLogger(ReadyForValidationConsumer.class);
    private final FhirContext fhirContext;
    private final ReportClient reportClient;
    private final ValidationService validationService;
    private final CategorizationService categorizationService;
    private final ResultRepository resultRepository;
    private final KafkaTemplate<String, ValidationComplete> validationCompleteTemplate;
    private final ValidationMetrics validationMetrics;
    private final BlobStorageService blobStorageService;
    private final PreQualificationConfig preQualificationConfig;
    private final PreQualOperationOutcomeBuilder preQualOperationOutcomeBuilder;

    public ReadyForValidationConsumer(
            FhirContext fhirContext,
            ReportClient reportClient,
            ValidationService validationService,
            CategorizationService categorizationService,
            ResultRepository resultRepository,
            KafkaTemplate<String, ValidationComplete> validationCompleteTemplate,
            ValidationMetrics validationMetrics,
            Optional<BlobStorageService> blobStorageService,
            PreQualificationConfig preQualificationConfig,
            PreQualOperationOutcomeBuilder preQualOperationOutcomeBuilder,
            ConsumerRecordRecoverer recoverer) {
        super(recoverer);
        this.fhirContext = fhirContext;
        this.reportClient = reportClient;
        this.validationService = validationService;
        this.categorizationService = categorizationService;
        this.resultRepository = resultRepository;
        this.validationCompleteTemplate = validationCompleteTemplate;
        this.validationMetrics = validationMetrics;
        this.blobStorageService = blobStorageService.orElse(null);
        this.preQualificationConfig = preQualificationConfig;
        this.preQualOperationOutcomeBuilder = preQualOperationOutcomeBuilder;
    }

    @Override
    protected void process(ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> record) {
        String correlationId = Headers.getCorrelationId(record.headers());
        _logger.debug("Processing {} message for facility {}, patient {}, report {}",
                LogUtils.sanitize(record.topic()),
                LogUtils.sanitize(record.key().getFacilityId()),
                LogUtils.sanitize(record.value().getPatientId()),
                LogUtils.sanitize(record.value().getReportTrackingId()));
        String facilityId = record.key().getFacilityId();
        String patientId = record.value().getPatientId();
        String reportId = record.value().getReportTrackingId();
        String payloadUri = record.value().getPayloadUri();
        Bundle bundle = getBundleFromBlobStorage(payloadUri);
        if (bundle == null) {
            bundle = getBundleViaRest(facilityId, patientId, reportId);
        }
        List<Result> results = validate(correlationId, facilityId, patientId, reportId, bundle);
        appendPreQualOperationOutcome(bundle, results, payloadUri);
        produceValidationCompleteRecord(correlationId, facilityId, patientId, reportId, results);
    }

    /**
     * When enabled, builds the pre-qualification OperationOutcome for the patient's unacceptable-category
     * findings and appends it to the same patient NDJSON blob in ABS. No-op when the flag is off, when
     * there is no blob storage or payload URI (e.g. local/dev), or when there are no unacceptable findings.
     */
    private void appendPreQualOperationOutcome(Bundle bundle, List<Result> results, String payloadUri) {
        if (!preQualificationConfig.isWritePreQualOperationOutcome()) {
            return;
        }
        if (blobStorageService == null || payloadUri == null) {
            _logger.debug("Pre-qual OperationOutcome enabled but no blob storage or payload URI; skipping append");
            return;
        }
        // Replay guard. The offset is acknowledged even when process() throws (AsyncListener), so a
        // failure after this append - producing ValidationComplete, say - sends the record to the retry
        // topic and process() runs again. The bundle is re-read from the same blob on that replay, so a
        // pre-qual OperationOutcome already present in it means we appended one previously and must not
        // append a second. Keyed on the oo-total extension, which only this writer emits: the Report
        // service's legacy flat OperationOutcome does not carry it and is correctly ignored here.
        if (hasPreQualOperationOutcome(bundle)) {
            _logger.info("Pre-qual OperationOutcome already present in the patient NDJSON; skipping append (replay)");
            return;
        }
        PreQualOperationOutcomeBuilder.MeasureReportRef measureReport =
                preQualOperationOutcomeBuilder.resolveMeasureReport(bundle);
        preQualOperationOutcomeBuilder
                .build(results, measureReport, preQualificationConfig.isWriteExpressionsInOperationOutcome())
                .ifPresent(operationOutcome -> {
                    String line = fhirContext.newJsonParser().encodeResourceToString(operationOutcome);
                    String blobName = BlobUrlParts.parse(payloadUri).getBlobName();
                    // blobName derives from the payloadUri carried on the Kafka record, so sanitize it
                    // before logging. The append uses the original, unmodified value.
                    _logger.debug("Appending pre-qual OperationOutcome ({} issues) to blob {}",
                            operationOutcome.getIssue().size(), LogUtils.sanitize(blobName));
                    blobStorageService.appendResource(blobName, line);
                });
    }

    /**
     * True when the bundle already carries a pre-qualification OperationOutcome written by this service,
     * identified by the {@code oo-total} extension that only this writer emits.
     */
    private boolean hasPreQualOperationOutcome(Bundle bundle) {
        if (bundle == null) {
            return false;
        }
        return bundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(resource -> resource instanceof OperationOutcome)
                .anyMatch(resource -> ((OperationOutcome) resource)
                        .getExtensionByUrl(PreQualOperationOutcomeBuilder.OO_TOTAL_URL) != null);
    }

    private Bundle getBundleFromBlobStorage(String payloadUri) {
        if (payloadUri == null) {
            _logger.debug("No payload URI; skipping retrieval from ABS");
            return null;
        }
        if (blobStorageService == null) {
            _logger.debug("No blob storage service; skipping retrieval from ABS");
            return null;
        }
        _logger.debug("Retrieving from ABS");
        String blobName = BlobUrlParts.parse(payloadUri).getBlobName();
        BinaryData data = blobStorageService.download(blobName);
        return fhirContext.newNDJsonParser().parseResource(Bundle.class, data.toStream());
    }

    private Bundle getBundleViaRest(String facilityId, String patientId, String reportId) {
        _logger.debug("Retrieving via REST");
        PatientSubmissionModel model;

        try {
            model = reportClient.getSubmissionModel(facilityId, patientId, reportId);
        } catch (Exception ex) {
            _logger.error("Unexpected error while retrieving submission model from report service: {}", ex.getMessage(), ex);
            throw ex;
        }

        return model.getBundle();
    }

    private List<Result> validate(String correlationId, String facilityId, String patientId, String reportId, Bundle bundle) {
        List<Result> results;

        Attributes attributes;

        try (Timer timer = Timer.start()) {
            results = validationService.validate(bundle);
            _logger.debug("Validation completed with {} results in {} seconds", results.size(), String.format("%.2f", timer.getSeconds()));

            attributes = buildMetricAttributes(bundle, results, correlationId, facilityId, patientId, reportId);
            validationMetrics.addToValidationCounter(attributes);
            validationMetrics.recordValidationDuration(timer.getMilliseconds(), attributes);
        }

        for (Result result : results) {
            result.setFacilityId(facilityId);
            result.setPatientId(patientId);
            result.setReportId(reportId);
        }

        try (Timer timer = Timer.start()) {
            categorizationService.categorize(results);
            validationMetrics.recordCategorizationDuration(timer.getMilliseconds(), attributes);
        }
        resultRepository.saveAll(results);
        return results;
    }

    private Attributes buildMetricAttributes(Bundle bundle, List<Result> results, String correlationId, String facilityId, String patientId, String reportId) {

        int resourceCount = bundle.getEntry().size();
        int totalIssueCount = 0;
        int uncategorizedIssueCount = 0;
        int acceptableIssueCount = 0;
        int unacceptableIssueCount = 0;
        for (Result result : results) {
            totalIssueCount++;
            List<Category> categories = result.getCategories();
            if (categories == null || categories.isEmpty()) {
                uncategorizedIssueCount++;
            } else {
                if (categories.stream().allMatch(Category::isAcceptable)) {
                    acceptableIssueCount++;
                } else {
                    unacceptableIssueCount++;
                }
            }
        }
        String validationOutcome = (uncategorizedIssueCount == 0 && unacceptableIssueCount == 0) ? "Passed" : "Failed";

        return Attributes.builder()
                .put(DiagnosticNames.CORRELATION_ID, correlationId)
                .put(DiagnosticNames.FACILITY_ID, facilityId)
                .put(DiagnosticNames.PATIENT_ID, patientId)
                .put(DiagnosticNames.REPORT_TRACKING_ID, reportId)
                .put(DiagnosticNames.RESOURCE_COUNT, resourceCount)
                .put(DiagnosticNames.VALIDATION_OUTCOME, validationOutcome)
                .put(DiagnosticNames.ISSUE_COUNT_TOTAL, totalIssueCount)
                .put(DiagnosticNames.ISSUE_COUNT_UNCATEGORIZED, uncategorizedIssueCount)
                .put(DiagnosticNames.ISSUE_COUNT_UNACCEPTABLE, unacceptableIssueCount)
                .put(DiagnosticNames.ISSUE_COUNT_ACCEPTABLE, acceptableIssueCount)
                .build();
    }

    private void produceValidationCompleteRecord(
            String correlationId,
            String facilityId,
            String patientId,
            String reportId,
            List<Result> results) {
        ValidationComplete value = new ValidationComplete();
        value.setPatientId(patientId);
        value.setReportTrackingId(reportId);
        value.setValid(results.stream()
                .flatMap(result -> result.getCategories().stream())
                .allMatch(Category::isAcceptable));
        org.apache.kafka.common.header.Headers headers = new RecordHeaders();
        if (correlationId != null) {
            headers.add(Headers.CORRELATION_ID, Headers.getBytes(correlationId));
        }
        try {
            // Use .get() to make the send synchronous and wait for broker confirmation
            validationCompleteTemplate.send(new ProducerRecord<>(Topics.VALIDATION_COMPLETE, null, facilityId, value, headers)).get();
        } catch (Exception e) {
            _logger.error("Failed to send ValidationComplete record for patient {} in report {}",
                    LogUtils.sanitize(patientId), LogUtils.sanitize(reportId), e);
            // Throwing the exception ensures AsyncListener's catch block handles it
            // (e.g., sending the source record to the Error topic)
            throw new RuntimeException("Failed to produce ValidationComplete message", e);
        }
    }
}
