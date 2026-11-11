package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.QueryType;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import com.lantanagroup.link.measureeval.records.EvaluationRequested;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import com.lantanagroup.link.shared.kafka.records.reportstatus.PatientReportingStatus;
import com.lantanagroup.link.shared.kafka.records.reportstatus.ReportStatusUpdated;
import com.lantanagroup.link.shared.kafka.records.reportstatus.ReportStatusUpdatedKey;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Objects;
import java.util.stream.Collectors;

/** Patient stage outcomes; terminal failures are called only by the dead-letter recoverer. */
@Service
public class ReportStatusUpdatedProducer {
    private static final String COMPLETED_AT = "X-Evaluation-Completed-At-";
    private static final String PROCESSING_TIME = "X-Evaluation-Processing-Time";
    private final KafkaTemplate<ReportStatusUpdatedKey, ReportStatusUpdated> template;
    private final PatientReportingEvaluationStatusRepository patientStatusRepository;

    public ReportStatusUpdatedProducer(
            @Qualifier("compressedKafkaTemplate") KafkaTemplate<ReportStatusUpdatedKey, ReportStatusUpdated> template,
            PatientReportingEvaluationStatusRepository patientStatusRepository) {
        this.template = template;
        this.patientStatusRepository = patientStatusRepository;
    }

    public void publishSuccess(PatientReportingEvaluationStatus patient, QueryType phase, long processingTime,
                               org.apache.kafka.common.header.Headers inboundHeaders) {
        PatientReportingStatus stage = stage(phase);
        var timestampHeader = inboundHeaders == null ? null : inboundHeaders.lastHeader(COMPLETED_AT + phase);
        OffsetDateTime timestamp = timestampHeader == null
                ? OffsetDateTime.now(ZoneOffset.UTC)
                : OffsetDateTime.parse(Headers.getString(timestampHeader.value()));
        if (timestampHeader == null && inboundHeaders != null) {
            inboundHeaders.add(COMPLETED_AT + phase, Headers.getBytes(timestamp.toString()));
        }
        String durationHeader = PROCESSING_TIME + "-" + phase;
        if (inboundHeaders != null) {
            var previousDuration = inboundHeaders.lastHeader(durationHeader);
            if (previousDuration == null) {
                inboundHeaders.add(durationHeader, Headers.getBytes(Long.toString(processingTime)));
            } else {
                processingTime = Long.parseLong(Headers.getString(previousDuration.value()));
            }
        }
        var reports = patient.getReports().stream().collect(Collectors.groupingBy(
                PatientReportingEvaluationStatus.Report::getReportTrackingId,
                java.util.LinkedHashMap::new, Collectors.toList()));
        for (var entry : reports.entrySet()) {
            boolean reportable = entry.getValue().stream().anyMatch(r -> Boolean.TRUE.equals(r.getReportable()));
            // Nonreportable reports already completed in the INITIAL phase.
            if (phase == QueryType.SUPPLEMENTAL && !reportable) continue;
            send(ReportStatusUpdated.builder()
                    .reportScheduleId(entry.getKey()).patientId(patient.getPatientId())
                    .correlationId(patient.getCorrelationId()).status(stage).success(true)
                    .reportable(phase == QueryType.INITIAL ? reportable : null)
                    .processingTime(processingTime).timestamp(timestamp).build(), inboundHeaders);
        }
    }

    public void noteFailedAttempt(ConsumerRecord<?, ?> record, long elapsedMillis) {
        record.headers().remove(PROCESSING_TIME);
        record.headers().add(PROCESSING_TIME, Headers.getBytes(Long.toString(elapsedMillis)));
    }

    public void publishFinalFailure(ConsumerRecord<?, ?> record, Exception failure) {
        String patientId;
        String correlationId;
        List<String> reportIds;
        PatientReportingStatus stage;
        if (record.value() instanceof AbstractResourceRecord resource && record.key() instanceof ResourceKey key) {
            if (resource.getQueryType() == null) return;
            stage = stage(resource.getQueryType());
            patientId = key.getPatientId();
            correlationId = resource.getCacheKey();
            reportIds = resource.getScheduledReports().stream().filter(Objects::nonNull)
                    .map(AbstractResourceRecord.ScheduledReport::getReportTrackingId).toList();
        } else if (record.value() instanceof EvaluationRequested evaluation) {
            stage = PatientReportingStatus.SUPPLEMENTAL_EVALUATION;
            patientId = evaluation.getPatientId();
            var correlation = record.headers().lastHeader(Headers.CORRELATION_ID);
            correlationId = correlation == null || correlation.value() == null
                    ? null : Headers.getString(correlation.value());
            reportIds = java.util.Collections.singletonList(evaluation.getReportTrackingId());
        } else {
            return; // Deserialization failures cannot identify a patient/report.
        }
        if (blank(patientId) || blank(correlationId)) return;
        if (record.value() instanceof AbstractResourceRecord resource
                && resource.getQueryType() == QueryType.SUPPLEMENTAL && record.key() instanceof ResourceKey key) {
            // As in publishSuccess, a schedule is eligible if any of its measures was reportable.
            reportIds = patientStatusRepository.findByFacilityIdAndCorrelationId(key.getFacilityId(), correlationId)
                    .map(patient -> patient.getReports().stream()
                            .filter(report -> Boolean.TRUE.equals(report.getReportable()))
                            .map(PatientReportingEvaluationStatus.Report::getReportTrackingId).toList())
                    .orElseGet(List::of);
        }
        var duration = record.headers().lastHeader(PROCESSING_TIME);
        Long processingTime = duration == null ? null : Long.valueOf(Headers.getString(duration.value()));
        OffsetDateTime timestamp = OffsetDateTime.now(ZoneOffset.UTC);
        String errorMessage = "Measure evaluation failed: "
                + (failure == null ? "Unknown error" : failure.getClass().getSimpleName());
        if (failure != null && !blank(failure.getMessage())) {
            errorMessage += ": " + LogUtils.sanitize(failure.getMessage());
        }
        for (String reportId : reportIds.stream().filter(id -> !blank(id)).distinct().toList()) {
            send(ReportStatusUpdated.builder().reportScheduleId(reportId).patientId(patientId)
                    .correlationId(correlationId).status(stage).success(false).processingTime(processingTime)
                    .errorMessage(errorMessage)
                    .timestamp(timestamp).build(), record.headers());
        }
    }

    private static PatientReportingStatus stage(QueryType phase) {
        return switch (phase) {
            case INITIAL -> PatientReportingStatus.INITIAL_EVALUATION;
            case SUPPLEMENTAL -> PatientReportingStatus.SUPPLEMENTAL_EVALUATION;
            default -> throw new IllegalArgumentException("Unsupported evaluation phase: " + phase);
        };
    }

    private static boolean blank(String value) {
        return value == null || value.isBlank();
    }

    private void send(ReportStatusUpdated event, org.apache.kafka.common.header.Headers inboundHeaders) {
        var headers = new RecordHeaders();
        headers.add(Headers.CORRELATION_ID, Headers.getBytes(event.getCorrelationId()));
        Headers.copyMetricsMode(inboundHeaders, headers);
        var key = new ReportStatusUpdatedKey(event.getReportScheduleId(), event.getPatientId());
        try {
            template.send(new ProducerRecord<>(Topics.REPORT_STATUS_UPDATED, null, key, event, headers)).get();
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
            throw new RuntimeException("Failed to produce ReportStatusUpdated message", ex);
        } catch (Exception ex) {
            throw new RuntimeException("Failed to produce ReportStatusUpdated message", ex);
        }
    }
}
