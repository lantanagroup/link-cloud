package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.EvaluationRequested;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.repositories.AbstractResourceRepository;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
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
import java.util.stream.Collectors;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

@Service
public class EvaluationRequestedConsumer {

    private static final Logger logger = LoggerFactory.getLogger(EvaluationRequestedConsumer.class);
    private final PatientReportingEvaluationStatusRepository patientStatusRepository;
    private final MeasureEvaluatorCache measureEvaluatorCache;
    private final MeasureEvalMetrics measureEvalMetrics;
    private final PatientStatusBundler patientStatusBundler;
    private final ResourceEvaluatedProducer resourceEvaluatedProducer;

    EvaluationRequestedConsumer(AbstractResourceRepository resourceRepository,
                                PatientReportingEvaluationStatusRepository patientStatusRepository,
                                MeasureEvaluatorCache measureEvaluatorCache,
                                MeasureReportNormalizer measureReportNormalizer,
                                Predicate<MeasureReport> reportabilityPredicate,
                                KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
                                @Qualifier("compressedKafkaTemplate")
                                KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate,
                                MeasureEvalMetrics measureEvalMetrics, PatientStatusBundler patientStatusBundler, ResourceEvaluatedProducer resourceEvaluatedProducer) {
        this.patientStatusRepository = patientStatusRepository;
        this.measureEvaluatorCache = measureEvaluatorCache;
        this.measureEvalMetrics = measureEvalMetrics;
        this.patientStatusBundler = patientStatusBundler;
        this.resourceEvaluatedProducer = resourceEvaluatedProducer;
    }

    @KafkaListener(topics = Topics.EVALUATION_REQUESTED)
    public void consume(@Header(Headers.REPORT_TRACKING_ID) String reportTrackingID,
                        ConsumerRecord<String, EvaluationRequested> record) {

        Span currentSpan = Span.current();
        MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
        MDC.put("spanId", currentSpan.getSpanContext().getSpanId());

        Attributes attributes = Attributes.builder().put(stringKey("reportTrackingID"), reportTrackingID).build();
        measureEvalMetrics.IncrementRecordsReceivedCounter(attributes);

        String facilityId = record.key();
        var patientReportStatuses = patientStatusRepository.findByFacilityIdAndReportTrackingId(facilityId, reportTrackingID);

        for (PatientReportingEvaluationStatus status: patientReportStatuses) {
            var bundle = patientStatusBundler.createBundle(status);
            evaluateMeasures(record.value(), status, bundle);
        }
    }

    private void evaluateMeasures (EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, Bundle bundle) {
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measures");
        }
        for (PatientReportingEvaluationStatus.Report report : patientStatus.getReports().stream().filter(r -> Objects.equals(r.getReportTrackingId(), value.getReportId())).toList()) {
            MeasureReport measureReport = evaluateMeasure(patientStatus, report, bundle);
//            produceResourceEvaluatedRecords(value.getQueryType(), patientStatus, report, measureReport);
            this.resourceEvaluatedProducer.produceResourceEvaluatedRecords(patientStatus, report, measureReport);
        }

        boolean reportablePatient = patientStatus.getReports().stream().anyMatch(PatientReportingEvaluationStatus.Report::getReportable);
        // if at least one reportable measure, increment the reportable patient counter otherwise increment the non-reportable patient counter
        //updatePatientMetrics(value, patientStatus, reportablePatient);
    }

//    private void updatePatientMetrics (EvaluationRequested value, PatientReportingEvaluationStatus patientStatus, boolean reportablePatient) {
//        Attributes attributes = Attributes.builder().put(stringKey("facilityId"), patientStatus.getFacilityId()).
//                    put(stringKey("patientId"), patientStatus.getPatientId()).
//                    put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();
//            if (reportablePatient) {
//                measureEvalMetrics.IncrementPatientReportableCounter(attributes);
//            } else {
//                measureEvalMetrics.IncrementPatientNonReportableCounter(attributes);
//            }
//
//    }

    private MeasureReport evaluateMeasure (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {

        long start = System.currentTimeMillis();

        String measureId = report.getReportType();
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measure: {}", measureId);
        }
        MeasureEvaluator measureEvaluator = measureEvaluatorCache.get(measureId);
        if (measureEvaluator == null) {
            throw new IllegalStateException(String.format("Unknown measure: %s", measureId));
        }
        MeasureReport measureReport = measureEvaluator.evaluate(
                report.getStartDate(),
                report.getEndDate(),
                patientStatus.getPatientId(),
                bundle);
        if (logger.isDebugEnabled()) {
            logger.debug("Population counts: {}", measureReport.getGroup().stream()
                    .flatMap(group -> group.getPopulation().stream())
                    .map(population -> String.format(
                            "%s=[%d]",
                            population.getCode().getCodingFirstRep().getCode(),
                            population.getCount()))
                    .collect(Collectors.joining(" ")));
        }

        long timeElapsed = System.currentTimeMillis() - start;
        Attributes attributes = Attributes.builder().put(stringKey("facilityId"), patientStatus.getFacilityId()).
                put(stringKey("patientId"), patientStatus.getPatientId()).
                put(stringKey("reportTypes"), report.getReportType()).
                put(stringKey("frequency"), report.getFrequency()).
                put(stringKey("startDate"), report.getStartDate().toString()).
                put(stringKey("endDate"), report.getEndDate().toString()).
                put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();
        if (logger.isInfoEnabled()) {
            logger.info("Measure evaluation duration for Patient {} : {}", patientStatus.getPatientId(), timeElapsed + " milliseconds");
        }

        // Record the duration of the evaluation
        measureEvalMetrics.MeasureEvalDuration(timeElapsed, attributes);

        return measureReport;
    }
}
