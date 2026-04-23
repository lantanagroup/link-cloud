package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.ReportableEvent;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.EvaluationRequested;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.measureeval.repositories.ResourceRepository;
import com.lantanagroup.link.shared.kafka.AsyncListener;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.trace.Span;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.stereotype.Service;

import java.util.Objects;
import java.util.UUID;
import java.util.function.Predicate;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

@Service
public class EvaluationRequestedConsumer extends AsyncListener<String, EvaluationRequested> {

    private static final Logger logger = LoggerFactory.getLogger(EvaluationRequestedConsumer.class);
    private final PatientReportingEvaluationStatusRepository patientStatusRepository;
    private final MeasureEvalMetrics measureEvalMetrics;
    private final PatientStatusBundler patientStatusBundler;
    private final MeasureReportGeneratedProducer measureReportGeneratedProducer;
    private final BlobStorageService blobStorageService;
    private final EvaluateMeasureService evaluateMeasureService;
    private final FhirContext fhirContext;

    EvaluationRequestedConsumer(ResourceRepository resourceRepository,
                                PatientReportingEvaluationStatusRepository patientStatusRepository,
                                Predicate<MeasureReport> reportabilityPredicate,
                                KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
                                MeasureEvalMetrics measureEvalMetrics,
                                PatientStatusBundler patientStatusBundler,
                                MeasureReportGeneratedProducer measureReportGeneratedProducer,
                                BlobStorageService blobStorageService,
                                EvaluateMeasureService evaluateMeasureService,
                                ConsumerRecordRecoverer recoverer, FhirContext fhirContext) {
        super(recoverer);
        this.patientStatusRepository = patientStatusRepository;
        this.measureEvalMetrics = measureEvalMetrics;
        this.patientStatusBundler = patientStatusBundler;
        this.measureReportGeneratedProducer = measureReportGeneratedProducer;
        this.blobStorageService = blobStorageService;
        this.evaluateMeasureService = evaluateMeasureService;
        this.fhirContext = fhirContext;
    }

    @Override
    protected void process(ConsumerRecord<String, EvaluationRequested> record) {
        String correlationId = Headers.getCorrelationId(record.headers());
        String queryType = Headers.getQueryType(record.headers());
        var reportTrackingId = record.value().getReportTrackingId();
        String facilityId = record.key();

        Span currentSpan = Span.current();
        MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
        MDC.put("spanId", currentSpan.getSpanContext().getSpanId());

        var patientReportStatus = patientStatusRepository.findByFacilityIdAndPatientIdAndReportsReportTrackingId(facilityId, record.value().getPatientId(), record.value().getPreviousReportId()).orElse(null);

        if (patientReportStatus != null) {
            var bundle = patientStatusBundler.createBundle(facilityId, patientReportStatus.getCorrelationId());

            if (queryType != null) {
                Attributes attributes = MeasureEvalMetrics.buildAttributes(queryType, patientReportStatus, reportTrackingId, bundle.getEntry().size());
                measureEvalMetrics.IncrementRecordsReceivedCounter(attributes);
            }

            evaluateMeasures(queryType, reportTrackingId, correlationId, record.value(), patientReportStatus, bundle);
        } else {
            logger.warn("Patient status not found for facilityId: {}, patientId: {}, reportTrackingId: {}. EvaluationRequested event not fully processed.", facilityId, record.value().getPatientId(), record.value().getPreviousReportId());
        }
    }

    private void evaluateMeasures (String queryType, String reportTrackingId, String correlationId, EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, Bundle bundle) {
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measures");
        }

        //get valid report in array
        var reports = patientStatus.getReports().stream().filter(r -> Objects.equals(r.getReportTrackingId(), value.getPreviousReportId())).toList();

        //create new PatientReportingEvaluationStatus and save it
        reports.forEach(r -> r.setReportTrackingId(reportTrackingId));
        var newPatientStatus = new PatientReportingEvaluationStatus();
        newPatientStatus.setFacilityId(patientStatus.getFacilityId());
        newPatientStatus.setPatientId(patientStatus.getPatientId());
        newPatientStatus.setCorrelationId(correlationId);
        newPatientStatus.setReportableEvent(ReportableEvent.ADHOC.name());
        newPatientStatus.setReports(reports);
        patientStatusRepository.insert(newPatientStatus);

        reports.forEach(r -> {
            MeasureReport measureReport = evaluateMeasureService.evaluateMeasure(queryType, patientStatus, r, bundle);

            if (measureReport.getIdPart() == null) {
                measureReport.setId(UUID.randomUUID().toString());
            }

            blobStorageService.storePatientInBlobStorage(patientStatus, r, measureReport);

            Attributes attributes = MeasureEvalMetrics.buildAttributes(queryType, patientStatus, value.getReportTrackingId(), bundle.getEntry().size());
            measureEvalMetrics.IncrementPatientReportableCounter(attributes, Boolean.TRUE.equals(r.getReportable()));
        });
    }
}
