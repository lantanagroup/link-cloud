package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.exceptions.ValidationException;
import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.models.ReportableEvent;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.EvaluationRequested;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.repositories.AbstractResourceRepository;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusTemplateRepository;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.trace.Span;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.messaging.handler.annotation.Header;
import org.springframework.stereotype.Service;

import java.util.Objects;
import java.util.function.Predicate;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

@Service
public class EvaluationRequestedConsumer {

    private static final Logger logger = LoggerFactory.getLogger(EvaluationRequestedConsumer.class);
    private final PatientReportingEvaluationStatusRepository patientStatusRepository;
    private final MeasureEvalMetrics measureEvalMetrics;
    private final PatientStatusBundler patientStatusBundler;
    private final ResourceEvaluatedProducer resourceEvaluatedProducer;
    private final EvaluateMeasureService evaluateMeasureService;
    private final PatientReportingEvaluationStatusTemplateRepository patientReportingEvaluationStatusTemplateRepository;

    EvaluationRequestedConsumer(AbstractResourceRepository resourceRepository,
                                PatientReportingEvaluationStatusRepository patientStatusRepository,
                                MeasureReportNormalizer measureReportNormalizer,
                                Predicate<MeasureReport> reportabilityPredicate,
                                KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
                                @Qualifier("compressedKafkaTemplate")
                                KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate,
                                MeasureEvalMetrics measureEvalMetrics,
                                PatientStatusBundler patientStatusBundler,
                                ResourceEvaluatedProducer resourceEvaluatedProducer,
                                EvaluateMeasureService evaluateMeasureService,
                                PatientReportingEvaluationStatusTemplateRepository patientReportingEvaluationStatusTemplateRepository) {
        this.patientStatusRepository = patientStatusRepository;
        this.measureEvalMetrics = measureEvalMetrics;
        this.patientStatusBundler = patientStatusBundler;
        this.resourceEvaluatedProducer = resourceEvaluatedProducer;
        this.evaluateMeasureService = evaluateMeasureService;
        this.patientReportingEvaluationStatusTemplateRepository = patientReportingEvaluationStatusTemplateRepository;
    }

    @KafkaListener(topics = Topics.EVALUATION_REQUESTED)
    public void consume(@Header(Headers.REPORT_TRACKING_ID) String reportTrackingID,
                        @Header(Headers.CORRELATION_ID) String correlationId,
                        ConsumerRecord<String, EvaluationRequested> record) {

        Span currentSpan = Span.current();
        MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
        MDC.put("spanId", currentSpan.getSpanContext().getSpanId());

        Attributes attributes = Attributes.builder().put(stringKey("reportTrackingID"), reportTrackingID).build();
        measureEvalMetrics.IncrementRecordsReceivedCounter(attributes);

        String facilityId = record.key();
        var patientReportStatus = patientReportingEvaluationStatusTemplateRepository.getFirstByFacilityIdAndPatientIdAndReports_ReportTrackingId(facilityId, record.value().getPatientId(), record.value().getPreviousReportId());

        if (patientReportStatus != null) {
            var bundle = patientStatusBundler.createBundle(patientReportStatus);
            evaluateMeasures(reportTrackingID, correlationId, record.value(), patientReportStatus, bundle);
        } else {
            logger.warn("Patient status not found for facilityId: {}, patientId: {}, reportTrackingId: {}. EvaluationRequested event not fully processed.", facilityId, record.value().getPatientId(), record.value().getPreviousReportId());
        }
    }

    private void evaluateMeasures (String reportTrackingId, String correlationId, EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, Bundle bundle) {
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measures");
        }

        //get valid report in array
        var reports = patientStatus.getReports().stream().filter(r -> Objects.equals(r.getReportTrackingId(), value.getPreviousReportId())).toList();

        if(reports.size() > 1){
            var message = String.format("Multiple reports found with the same reportTrackingId: %s", value.getPreviousReportId());
            throw new ValidationException(message);
        }

        //create new PatientReportingEvaluationStatus and save it
        reports.forEach(r -> r.setReportTrackingId(reportTrackingId));
        var newPatientStatus = new PatientReportingEvaluationStatus();
        newPatientStatus.setFacilityId(patientStatus.getFacilityId());
        newPatientStatus.setPatientId(patientStatus.getPatientId());
        newPatientStatus.setCorrelationId(correlationId);
        newPatientStatus.setReportableEvent(ReportableEvent.ADHOC.name());
        newPatientStatus.setReports(reports);
        newPatientStatus.setResources(patientStatus.getResources());
        patientStatusRepository.insert(newPatientStatus);

        reports.forEach(r -> {
            MeasureReport measureReport = evaluateMeasureService.evaluateMeasure(patientStatus, r, bundle);
            this.resourceEvaluatedProducer.produceResourceEvaluatedRecords(patientStatus, r, measureReport);
        });

        boolean reportablePatient = patientStatus.getReports().stream().anyMatch(PatientReportingEvaluationStatus.Report::getReportable);
        // if at least one reportable measure, increment the reportable patient counter otherwise increment the non-reportable patient counter
        updatePatientMetrics(value, patientStatus, reportablePatient);
    }

    private void updatePatientMetrics (EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, boolean reportablePatient) {
        Attributes attributes = Attributes.builder().put(stringKey("facilityId"), patientStatus.getFacilityId()).
                    put(stringKey("patientId"), patientStatus.getPatientId()).
                    put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();
            if (reportablePatient) {
                measureEvalMetrics.IncrementPatientReportableCounter(attributes);
            } else {
                measureEvalMetrics.IncrementPatientNonReportableCounter(attributes);
            }

    }
}
