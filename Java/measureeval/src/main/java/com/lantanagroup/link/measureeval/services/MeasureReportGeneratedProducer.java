package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.records.MeasureReportGenerated;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

@Service
public class MeasureReportGeneratedProducer {
    private static final Logger logger = LoggerFactory.getLogger(MeasureReportGeneratedProducer.class);
    @Qualifier("compressedKafkaTemplate")
    private final KafkaTemplate<String, MeasureReportGenerated> resourceEvaluatedTemplate;

    public MeasureReportGeneratedProducer(KafkaTemplate<String, MeasureReportGenerated> resourceEvaluatedTemplate) {
        this.resourceEvaluatedTemplate = resourceEvaluatedTemplate;
    }

    public void produceMeasureReportGeneratedRecord (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            String measureReportId,
            String payloadUri,
            String blobName) {

        if (patientStatus == null || report == null || report.getReportTrackingId() == null || measureReportId == null) {
            throw new IllegalArgumentException("All parameters are required");
        }

        logger.info(
                "PRODUCING MeasureReportGenerated: REPORT_TYPE=[{}] REPORT_TRACKING_ID=[{}] REPORTABLE=[{}]",
                report.getReportType(),
                report.getReportTrackingId(),
                report.getReportable());

        String reportUri = null;

        if (payloadUri != null) {
            reportUri = payloadUri;
        }

        MeasureReportGenerated value = new MeasureReportGenerated(
                measureReportId,
                patientStatus.getFacilityId(),
                report.getReportTrackingId(),
                patientStatus.getPatientId(),
                report.getReportType(),
                reportUri,
                blobName,
                report.getReportable()
        );

        org.apache.kafka.common.header.Headers headers = new RecordHeaders()
                .add(Headers.CORRELATION_ID, Headers.getBytes(patientStatus.getCorrelationId()));

        try {
            resourceEvaluatedTemplate.send(new ProducerRecord<>(
                    Topics.MEASURE_REPORT_GENERATED,
                    null,
                    null,
                    null,
                    value,
                    headers));
        } catch (Exception ex) {
            logger.error("Failed to produce measure report generated record: {}", ex.getMessage());
            throw ex;
        }
    }
}