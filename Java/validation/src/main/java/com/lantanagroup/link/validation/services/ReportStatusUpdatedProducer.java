package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.kafka.records.reportstatus.PatientReportingStatus;
import com.lantanagroup.link.shared.kafka.records.reportstatus.ReportStatusUpdated;
import com.lantanagroup.link.shared.kafka.records.reportstatus.ReportStatusUpdatedKey;
import com.lantanagroup.link.shared.kafka.records.reportstatus.ValidationResult;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

import java.time.Clock;
import java.time.OffsetDateTime;

/**
 * Publishes a patient's Validation-stage {@link ReportStatusUpdated} event.
 * The occurrence timestamp is set once, when the event is built, and the same record is sent again
 * if the first send fails. See dev-docs/report-status-events.md.
 */
@Service
public class ReportStatusUpdatedProducer {
    private static final Logger logger = LoggerFactory.getLogger(ReportStatusUpdatedProducer.class);

    private final KafkaTemplate<ReportStatusUpdatedKey, ReportStatusUpdated> template;
    private final Clock clock;

    @Autowired
    public ReportStatusUpdatedProducer(
            @Qualifier("defaultKafkaTemplate") KafkaTemplate<ReportStatusUpdatedKey, ReportStatusUpdated> template) {
        this(template, Clock.systemUTC());
    }

    ReportStatusUpdatedProducer(
            KafkaTemplate<ReportStatusUpdatedKey, ReportStatusUpdated> template,
            Clock clock) {
        this.template = template;
        this.clock = clock;
    }

    public void publishSuccess(
            String reportScheduleId,
            String patientId,
            String correlationId,
            long processingTimeMillis,
            boolean passed,
            long messageCount,
            org.apache.kafka.common.header.Headers inboundHeaders) {
        ReportStatusUpdated event = baseEvent(reportScheduleId, patientId, correlationId, processingTimeMillis)
                .success(true)
                .validationResult(ValidationResult.builder()
                        .passed(passed)
                        .messageCount(messageCount)
                        .build())
                .build();
        send(event, inboundHeaders);
    }

    public void publishFailure(
            String reportScheduleId,
            String patientId,
            String correlationId,
            long processingTimeMillis,
            String errorMessage,
            org.apache.kafka.common.header.Headers inboundHeaders) {
        ReportStatusUpdated event = baseEvent(reportScheduleId, patientId, correlationId, processingTimeMillis)
                .success(false)
                .errorMessage(errorMessage)
                .build();
        send(event, inboundHeaders);
    }

    private ReportStatusUpdated.ReportStatusUpdatedBuilder baseEvent(
            String reportScheduleId,
            String patientId,
            String correlationId,
            long processingTimeMillis) {
        return ReportStatusUpdated.builder()
                .reportScheduleId(reportScheduleId)
                .patientId(patientId)
                .status(PatientReportingStatus.VALIDATION)
                .processingTime(processingTimeMillis)
                .correlationId(correlationId)
                .timestamp(OffsetDateTime.now(clock));
    }

    private void send(ReportStatusUpdated event, org.apache.kafka.common.header.Headers inboundHeaders) {
        // Leave the partition unset so the default murmur2 partitioner hashes the key bytes.
        // partitioner.ignore.keys must stay off: every service uses this same key for the patient.
        ReportStatusUpdatedKey key = ReportStatusUpdatedKey.builder()
                .reportId(event.getReportScheduleId())
                .patientId(event.getPatientId())
                .build();
        ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> record = new ProducerRecord<>(
                Topics.REPORT_STATUS_UPDATED,
                null,
                key,
                event,
                headers(event.getCorrelationId(), inboundHeaders));
        try {
            doSend(record);
        } catch (RuntimeException first) {
            if (isInterrupted(first)) {
                throw first;
            }
            logger.warn("Retrying ReportStatusUpdated for report {} patient {}",
                    LogUtils.sanitize(event.getReportScheduleId()),
                    LogUtils.sanitize(event.getPatientId()),
                    first);
            try {
                doSend(record);
            } catch (RuntimeException second) {
                logger.error("Failed to produce ReportStatusUpdated for report {} patient {}",
                        LogUtils.sanitize(event.getReportScheduleId()),
                        LogUtils.sanitize(event.getPatientId()),
                        second);
                throw second;
            }
        }
    }

    private void doSend(ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> record) {
        try {
            template.send(record).get();
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
            throw new RuntimeException("Failed to produce ReportStatusUpdated message", ex);
        } catch (Exception ex) {
            throw new RuntimeException("Failed to produce ReportStatusUpdated message", ex);
        }
    }

    private static boolean isInterrupted(Throwable failure) {
        Throwable current = failure;
        while (current != null) {
            if (current instanceof InterruptedException) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    private static org.apache.kafka.common.header.Headers headers(
            String correlationId,
            org.apache.kafka.common.header.Headers inboundHeaders) {
        org.apache.kafka.common.header.Headers headers = new RecordHeaders();
        if (correlationId != null) {
            headers.add(Headers.CORRELATION_ID, Headers.getBytes(correlationId));
        }
        Headers.copyMetricsMode(inboundHeaders, headers);
        return headers;
    }
}
