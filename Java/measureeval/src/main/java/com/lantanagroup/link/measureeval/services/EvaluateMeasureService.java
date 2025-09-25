package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import com.lantanagroup.link.shared.utils.LogUtils;
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

    // Overload without queryType just calls the unified one
    public MeasureReport evaluateMeasure(
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {
        return evaluateMeasure(null, patientStatus, report, bundle);
    }

    // Unified method (handles both cases)
    public MeasureReport evaluateMeasure(
            String queryType,
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {

        long start = System.currentTimeMillis();

        try {
            MeasureReport measureReport = doReportGeneration(patientStatus, report, bundle);

            logPopulationCounts(measureReport);

            long timeElapsed = System.currentTimeMillis() - start;

            Attributes attributes = buildAttributes(queryType, patientStatus, report);

            if (logger.isInfoEnabled()) {
                logger.info("Measure evaluation duration for Patient {}{}: {} ms",
                        safe(patientStatus.getPatientId()),
                        queryType != null ? " on " + queryType + " query" : "",
                        timeElapsed);
            }

            measureEvalMetrics.MeasureEvalDuration(timeElapsed, attributes);
            return measureReport;

        } catch (Exception ex) {
            logger.error("Measure evaluation failed [measure={}, patient={}, facility={}, correlationId={}]: {}",
                    report.getReportType(),
                    safe(patientStatus.getPatientId()),
                    safe(patientStatus.getFacilityId()),
                    safe(patientStatus.getCorrelationId()),
                    ex.getMessage(),
                    ex);
            throw ex;
        }
    }

    private MeasureReport doReportGeneration(PatientReportingEvaluationStatus patientStatus,
                                             PatientReportingEvaluationStatus.Report report,
                                             Bundle bundle) {
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

    private Attributes buildAttributes(String queryType,
                                       PatientReportingEvaluationStatus patientStatus,
                                       PatientReportingEvaluationStatus.Report report) {
        Attributes attributes = Attributes.builder()
                .put(stringKey(DiagnosticNames.FACILITY_ID), safe(patientStatus.getFacilityId()))
                .put(stringKey(DiagnosticNames.PATIENT_ID), safe(patientStatus.getPatientId()))
                .put(stringKey(DiagnosticNames.REPORT_TYPE), safe(report.getReportType()))
                .put(stringKey(DiagnosticNames.FREQUENCY), safe(report.getFrequency()))
                .put(stringKey(DiagnosticNames.PERIOD_START), safeDate(report.getStartDate()))
                .put(stringKey(DiagnosticNames.PERIOD_END), safeDate(report.getEndDate()))
                .put(stringKey(DiagnosticNames.CORRELATION_ID), safe(patientStatus.getCorrelationId())).build();
        if (queryType != null) {
            attributes = attributes.toBuilder().put(stringKey(DiagnosticNames.QUERY_TYPE), queryType).build();
        }
        return attributes;
    }

    private void logPopulationCounts(MeasureReport measureReport) {
        if (logger.isDebugEnabled()) {
            String counts = measureReport.getGroup().stream()
                    .flatMap(group -> group.getPopulation().stream())
                    .map(pop -> String.format("%s=[%d]",
                            pop.getCode().getCodingFirstRep().getCode(),
                            pop.getCount()))
                    .collect(Collectors.joining(" "));
            logger.debug("Population counts: {}", counts);
        }
    }

    private static String safe(String v) {
        String s = LogUtils.sanitize(v);
        return (s == null) ? "" : s;
    }

    private static String safeDate(Object date) {
        return (date == null) ? "" : date.toString();
    }
}

