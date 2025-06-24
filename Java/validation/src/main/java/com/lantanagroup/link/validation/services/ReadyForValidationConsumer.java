package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobUrlParts;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.PatientSubmissionModel;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import com.lantanagroup.link.validation.records.ValidationComplete;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import io.opentelemetry.api.common.Attributes;
import org.apache.commons.lang3.StringUtils;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.messaging.handler.annotation.Header;
import org.springframework.stereotype.Service;
import org.springframework.web.client.HttpServerErrorException;

import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.Optional;

@Service
public class ReadyForValidationConsumer {
    private final Logger _logger = LoggerFactory.getLogger(ReadyForValidationConsumer.class);
    private final FhirContext fhirContext;
    private final ReportClient reportClient;
    private final ValidationService validationService;
    private final CategorizationService categorizationService;
    private final ResultRepository resultRepository;
    private final KafkaTemplate<String, ValidationComplete> validationCompleteTemplate;
    private final ValidationMetrics validationMetrics;
    private final BlobStorageService blobStorageService;

    public ReadyForValidationConsumer(
            FhirContext fhirContext,
            ReportClient reportClient,
            ValidationService validationService,
            CategorizationService categorizationService,
            ResultRepository resultRepository,
            KafkaTemplate<String, ValidationComplete> validationCompleteTemplate,
            ValidationMetrics validationMetrics,
            Optional<BlobStorageService> blobStorageService) {
        this.fhirContext = fhirContext;
        this.reportClient = reportClient;
        this.validationService = validationService;
        this.categorizationService = categorizationService;
        this.resultRepository = resultRepository;
        this.validationCompleteTemplate = validationCompleteTemplate;
        this.validationMetrics = validationMetrics;
        this.blobStorageService = blobStorageService.orElse(null);
    }

    @KafkaListener(topics = Topics.READY_FOR_VALIDATION)
    public void consume(
            @Header(Headers.CORRELATION_ID) String correlationId,
            ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> record) {
        try {
            _logger.debug("Processing {} message for facility {}, patient {}, report {}",
                    record.topic(), record.key().getFacilityId(), record.value().getPatientId(), record.value().getReportTrackingId());
            String facilityId = record.key().getFacilityId();
            String patientId = record.value().getPatientId();
            String reportId = record.value().getReportTrackingId();
            Bundle bundle = getBundleFromBlobStorage(record.value().getPayloadUri());
            if (bundle == null) {
                bundle = getBundleViaRest(facilityId, patientId, reportId);
            }
            Instant start = Instant.now();
            List<Result> results = validate(facilityId, patientId, reportId, bundle);
            Instant end = Instant.now();
            Duration duration = Duration.between(start, end);
            produceValidationCompleteRecord(correlationId, facilityId, patientId, reportId, results);
            produceMetrics(correlationId, facilityId, patientId, reportId, bundle, results, duration);
        } catch (HttpServerErrorException ex) {
            _logger.error("HTTP error while processing message: {}", ex.getResponseBodyAsString(), ex);
        } catch (Exception ex) {
            _logger.error("Unexpected error while processing message", ex);
            throw ex; // Re-throw to allow Kafka to handle retries
        }
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

        IParser parser = fhirContext.newJsonParser();
        Bundle patientResources = parser.parseResource(Bundle.class, model.getPatientResources());
        if (StringUtils.isNotEmpty(model.getOtherResources())) {
            Bundle otherResources = parser.parseResource(Bundle.class, model.getOtherResources());
            patientResources.getEntry().addAll(otherResources.getEntry());
        }
        return patientResources;
    }

    private List<Result> validate(String facilityId, String patientId, String reportId, Bundle bundle) {
        List<Result> results = validationService.validate(bundle);
        for (Result result : results) {
            result.setFacilityId(facilityId);
            result.setPatientId(patientId);
            result.setReportId(reportId);
        }
        categorizationService.categorize(results);
        resultRepository.saveAll(results);
        return results;
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
        org.apache.kafka.common.header.Headers headers = new RecordHeaders()
                .add(Headers.CORRELATION_ID, Headers.getBytes(correlationId));
        validationCompleteTemplate.send(new ProducerRecord<>(Topics.VALIDATION_COMPLETE, null, facilityId, value, headers));
    }

    private void produceMetrics(
            String correlationId,
            String facilityId,
            String patientId,
            String reportId,
            Bundle bundle,
            List<Result> results,
            Duration duration) {
        int resourceCount = bundle.getEntry().size();
        int totalIssueCount = 0;
        int uncategorizedIssueCount = 0;
        int acceptableIssueCount = 0;
        int unacceptableIssueCount = 0;
        for (Result result : results) {
            totalIssueCount++;
            List<Category> categories = result.getCategories();
            if (categories.isEmpty()) {
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
        Attributes attributes = Attributes.builder()
                .put(DiagnosticNames.CORRELATION_ID, correlationId)
                .put(DiagnosticNames.FACILITY_ID, facilityId)
                .put(DiagnosticNames.PATIENT_ID, patientId)
                .put(DiagnosticNames.REPORT_ID, reportId)
                .put(DiagnosticNames.RESOURCE_COUNT, resourceCount)
                .put(DiagnosticNames.VALIDATION_OUTCOME, validationOutcome)
                .put(DiagnosticNames.ISSUE_COUNT_TOTAL, totalIssueCount)
                .put(DiagnosticNames.ISSUE_COUNT_UNCATEGORIZED, uncategorizedIssueCount)
                .put(DiagnosticNames.ISSUE_COUNT_UNACCEPTABLE, unacceptableIssueCount)
                .put(DiagnosticNames.ISSUE_COUNT_ACCEPTABLE, acceptableIssueCount)
                .build();
        validationMetrics.addToValidationCounter(attributes);
        validationMetrics.recordValidationDuration(duration.toMillis(), attributes);
    }
}
