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
import org.apache.commons.lang3.StringUtils;
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
    private final Predicate<MeasureReport> reportabilityPredicate;
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
        this.reportabilityPredicate = reportabilityPredicate;
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

        Span currentSpan = Span.current();
        MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
        MDC.put("spanId", currentSpan.getSpanContext().getSpanId());

        String facilityId = record.key();
        Attributes attributes = Attributes.builder().put(stringKey(DiagnosticNames.FACILITY_ID), facilityId).build();
        measureEvalMetrics.IncrementRecordsReceivedCounter(attributes);
        var patientReportStatus = patientStatusRepository.findByFacilityIdAndPatientIdAndReportsReportTrackingId(facilityId, record.value().getPatientId(), record.value().getPreviousReportId()).orElse(null);

        if (patientReportStatus != null) {
            var bundle = patientStatusBundler.createBundle(facilityId, patientReportStatus.getCorrelationId());
            evaluateMeasures(correlationId, record.value(), patientReportStatus, bundle, record.headers());
        } else {
            logger.warn("Patient status not found for facilityId: {}, patientId: {}, reportTrackingId: {}. EvaluationRequested event not fully processed.", facilityId, record.value().getPatientId(), record.value().getPreviousReportId());
            throw new IllegalStateException("Patient status not found for previous report ID");
        }
    }

    private void evaluateMeasures (String correlationId, EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, Bundle bundle) {
        evaluateMeasures(correlationId, value, patientStatus, bundle, null);
    }

    private void evaluateMeasures (String correlationId, EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, Bundle bundle, org.apache.kafka.common.header.Headers inboundHeaders) {
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measures");
        }

        //create new PatientReportingEvaluationStatus and save it
        var newPatientStatus = new PatientReportingEvaluationStatus();
        newPatientStatus.setFacilityId(patientStatus.getFacilityId());
        newPatientStatus.setPatientId(patientStatus.getPatientId());
        newPatientStatus.setCorrelationId(correlationId);
        newPatientStatus.setReportableEvent(ReportableEvent.ADHOC.name());
        newPatientStatus.setReports(patientStatus.getReports().stream()
                .filter(r -> StringUtils.equals(r.getReportTrackingId(), value.getPreviousReportId()))
                .map(r -> {
                    var report = new PatientReportingEvaluationStatus.Report();
                    report.setReportType(r.getReportType());
                    report.setFrequency(r.getFrequency());
                    report.setStartDate(r.getStartDate());
                    report.setEndDate(r.getEndDate());
                    report.setReportTrackingId(value.getReportTrackingId());
                    return report;
                })
                .toList());
        patientStatusRepository.insert(newPatientStatus);

        for (PatientReportingEvaluationStatus.Report r : newPatientStatus.getReports()) {
            MeasureReport measureReport;
            if (bundle.hasEntry()) {
                measureReport = evaluateMeasureService.evaluateMeasure(newPatientStatus, r, bundle);
                if (measureReport.getIdPart() == null) {
                    measureReport.setId(UUID.randomUUID().toString());
                }
            } else {
                measureReport = null;
            }

            boolean reportable = measureReport != null && reportabilityPredicate.test(measureReport);
            r.setReportable(reportable);
            if (reportable) {
                blobStorageService.storePatientInBlobStorage(newPatientStatus, r, measureReport, inboundHeaders);
            } else {
                String measureReportId = measureReport == null ? UUID.randomUUID().toString() : measureReport.getIdPart();
                measureReportGeneratedProducer.produceMeasureReportGeneratedRecord(newPatientStatus, r, measureReportId, null, null, inboundHeaders);
            }
        }

        patientStatusRepository.save(newPatientStatus);
        boolean reportablePatient = newPatientStatus.getReports().stream().anyMatch(PatientReportingEvaluationStatus.Report::getReportable);

        // if at least one reportable measure, increment the reportable patient counter otherwise increment the non-reportable patient counter
        updatePatientMetrics(value, newPatientStatus, reportablePatient);
    }

    private void updatePatientMetrics (EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, boolean reportablePatient) {
        Attributes attributes = MeasureEvalMetrics.buildPatientOutcomeAttributes(null, patientStatus);
            measureEvalMetrics.IncrementPatientReportableCounter(attributes, reportablePatient);

    }
}
