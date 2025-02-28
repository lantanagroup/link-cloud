package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import io.opentelemetry.api.common.Attributes;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.stream.Collectors;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

@Service
public class EvaluateMeasureService {

    private static final Logger logger = LoggerFactory.getLogger(EvaluateMeasureService.class);
    private final MeasureEvaluatorCache measureEvaluatorCache;
    private final MeasureEvalMetrics measureEvalMetrics;

    public EvaluateMeasureService(MeasureEvaluatorCache measureEvaluatorCache, MeasureEvalMetrics measureEvalMetrics) {
        this.measureEvaluatorCache = measureEvaluatorCache;
        this.measureEvalMetrics = measureEvalMetrics;
    }

    public MeasureReport evaluateMeasure (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {

        long start = System.currentTimeMillis();

        MeasureReport measureReport = doReportGeneration(patientStatus, report, bundle);

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

    public MeasureReport evaluateMeasure (
            String queryType,
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {
        long start = System.currentTimeMillis();

        MeasureReport measureReport = doReportGeneration(patientStatus, report, bundle);

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
                put(stringKey("queryType"), queryType).
                put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();
        if (logger.isInfoEnabled()) {
            logger.info("Measure evaluation duration for Patient {} on {} query: {}", patientStatus.getPatientId(), queryType, timeElapsed + " milliseconds");
        }

        // Record the duration of the evaluation
        measureEvalMetrics.MeasureEvalDuration(timeElapsed, attributes);

        return measureReport;
    }

    private MeasureReport doReportGeneration(PatientReportingEvaluationStatus patientStatus,
                                             PatientReportingEvaluationStatus.Report report,
                                             Bundle bundle){
        String measureId = report.getReportType();
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measure: {}", measureId);
        }
        MeasureEvaluator measureEvaluator = measureEvaluatorCache.get(measureId);
        if (measureEvaluator == null) {
            throw new IllegalStateException(String.format("Unknown measure: %s", measureId));
        }
        return measureEvaluator.evaluate(
                report.getStartDate(),
                report.getEndDate(),
                patientStatus.getPatientId(),
                bundle);
    }
}
