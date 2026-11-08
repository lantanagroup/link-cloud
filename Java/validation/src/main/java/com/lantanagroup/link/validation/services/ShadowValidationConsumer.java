package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobUrlParts;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.entities.PatientSubmissionModel;
import com.lantanagroup.link.shared.kafka.AsyncListener;
import com.lantanagroup.link.shared.services.ReportClient;
import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.SubjectDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.records.BridgeOutcome;
import com.lantanagroup.link.validation.records.ShadowCompareEvent;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.Bundle;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.stereotype.Service;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;

/**
 * Consumes {@link ShadowCompareEvent} on its own topic and consumer group, runs whichever engine the
 * primary consumer didn't run, and compares. Isolated from {@code ReadyForValidationConsumer} -- its own
 * topic and group mean its pace and failures can't affect the primary's, and every exception here is
 * caught and logged, never rethrown.
 */
@Service
public class ShadowValidationConsumer extends AsyncListener<String, ShadowCompareEvent> {
    private final Logger _logger = LoggerFactory.getLogger(ShadowValidationConsumer.class);
    private final FhirContext fhirContext;
    private final ReportClient reportClient;
    private final ValidationService validationService;
    private final CategorizationService categorizationService;
    private final BlobStorageService blobStorageService;
    private final RubricExecutionService rubricExecutionService;
    private final LegacyResultMapper legacyResultMapper;
    private final ObjectMapper objectMapper;
    private final LegacyShadowResultPersister legacyShadowResultPersister;
    private final ShadowComparator shadowComparator;
    private final String rubricId;

    public ShadowValidationConsumer(
            FhirContext fhirContext,
            ReportClient reportClient,
            ValidationService validationService,
            CategorizationService categorizationService,
            Optional<BlobStorageService> blobStorageService,
            RubricExecutionService rubricExecutionService,
            LegacyResultMapper legacyResultMapper,
            ObjectMapper objectMapper,
            LegacyShadowResultPersister legacyShadowResultPersister,
            ShadowComparator shadowComparator,
            @Value("${vaas.bridge.rubric-id:measure-report-submission-v1}") String rubricId,
            @Qualifier("deadLetterPublishingRecoverer")  ConsumerRecordRecoverer recoverer) {
        super(recoverer);
        this.fhirContext = fhirContext;
        this.reportClient = reportClient;
        this.validationService = validationService;
        this.categorizationService = categorizationService;
        this.blobStorageService = blobStorageService.orElse(null);
        this.rubricExecutionService = rubricExecutionService;
        this.legacyResultMapper = legacyResultMapper;
        this.objectMapper = objectMapper;
        this.legacyShadowResultPersister = legacyShadowResultPersister;
        this.shadowComparator = shadowComparator;
        this.rubricId = rubricId;
    }

    @Override
    protected void process(ConsumerRecord<String, ShadowCompareEvent> record) {
        ShadowCompareEvent event = record.value();
        try {
            Bundle bundle = resolveBundle(event.getPayloadUri(), event.getFacilityId(),
                    event.getPatientId(), event.getReportId());

            List<Result> legacyResults;
            List<Result> newResults;
            Boolean legacyValid;
            Boolean rubricValid;

            if (event.isRanNewEngine()) {
                // primary ran the new engine; we run the old one, purely for comparison
                OffsetDateTime requestedAt = OffsetDateTime.now();
                legacyResults = runLegacyValidation(event.getFacilityId(), event.getPatientId(),
                        event.getReportId(), bundle);
                OffsetDateTime completedAt = OffsetDateTime.now();
                legacyShadowResultPersister.persist(event.getRequestId(), event.getCorrelationId(),
                        event.getFacilityId(), event.getPatientId(), event.getReportId(), legacyResults,
                        requestedAt, completedAt);
                newResults = toResults(event.getAuthoritativeResult());
                legacyValid = ValidationVerdict.legacyValid(legacyResults);
                rubricValid = event.getAuthoritativeValid();
            } else {
                // primary ran the old engine; we run the new one -- persists to rubric_result/rubric_finding
                // exactly as it does when the modern engine is authoritative
                ValidationResultEnvelope envelope = runModernValidation(event.getCorrelationId(), event.getFacilityId(),
                        event.getPatientId(), event.getReportId(), bundle);
                BridgeOutcome outcome = legacyResultMapper.toResults(envelope, event.getFacilityId(),
                        event.getPatientId(), event.getReportId());
                newResults = outcome.results();
                legacyResults = toResults(event.getAuthoritativeResult());
                legacyValid = event.getAuthoritativeValid();
                // As the primary consumer decides it: by the rubric status, or unknown when there is none.
                rubricValid = outcome.status() == null ? null : ValidationVerdict.rubricValid(outcome.status());
            }

            shadowComparator.compareAndLog(event.getCorrelationId(), event.getRequestId(), event.getFacilityId(),
                    event.getPatientId(), event.getReportId(), rubricId, event.isRanNewEngine(), legacyValid, rubricValid,
                    legacyResults, newResults);
        } catch (Exception e) {
            _logger.warn("Shadow comparison failed for report {}; skipping",
                    LogUtils.sanitize(event.getReportId()), e);
        }
    }

    private Bundle resolveBundle(String payloadUri, String facilityId, String patientId, String reportId) {
        Bundle bundle = getBundleFromBlobStorage(payloadUri);
        if (bundle == null) {
            bundle = getBundleViaRest(facilityId, patientId, reportId);
        }
        return bundle;
    }

    private Bundle getBundleFromBlobStorage(String payloadUri) {
        if (payloadUri == null || blobStorageService == null) {
            return null;
        }
        String blobName = BlobUrlParts.parse(payloadUri).getBlobName();
        BinaryData data = blobStorageService.download(blobName);
        return fhirContext.newNDJsonParser().parseResource(Bundle.class, data.toStream());
    }

    private Bundle getBundleViaRest(String facilityId, String patientId, String reportId) {
        PatientSubmissionModel model = reportClient.getSubmissionModel(facilityId, patientId, reportId);
        return model.getBundle();
    }

    private List<Result> runLegacyValidation(String facilityId, String patientId, String reportId, Bundle bundle) {
        List<Result> results = validationService.validate(bundle);
        for (Result result : results) {
            result.setFacilityId(facilityId);
            result.setPatientId(patientId);
            result.setReportId(reportId);
        }
        categorizationService.categorize(results);
        return results;
    }

    private ValidationResultEnvelope runModernValidation(String correlationId, String facilityId, String patientId, String reportId, Bundle bundle) {
        SubjectDto subject = SubjectDto.builder()
                .facilityId(facilityId)
                .patientId(patientId)
                .reportId(reportId)
                .build();
        // The bundle as already parsed; encoding it to JSON for the engine to parse again doubled the parsing cost.
        return rubricExecutionService.evaluate(rubricId, null, subject, bundle, true, correlationId);
    }

    private List<Result> toResults(List<ShadowFindingDto> findings) {
        if (findings == null) {
            return List.of();
        }
        return findings.stream().map(ShadowFindingDto::toResult).toList();
    }
}
