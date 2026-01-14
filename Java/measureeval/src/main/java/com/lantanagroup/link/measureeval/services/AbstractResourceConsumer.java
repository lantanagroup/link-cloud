package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.repositories.ResourceRepository;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.kafka.AsyncListener;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.measureeval.entities.QueryType;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.trace.Span;
import org.apache.commons.collections4.map.PassiveExpiringMap;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.KafkaUtils;
import org.springframework.util.StopWatch;

import java.util.*;
import java.util.concurrent.TimeUnit;
import java.util.function.Predicate;
import java.util.stream.Collectors;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

public abstract class AbstractResourceConsumer<T extends AbstractResourceRecord> extends AsyncListener<String, T> {
    private static final Logger logger = LoggerFactory.getLogger(AbstractResourceConsumer.class);
    private static final Logger performanceLogger =LoggerFactory.getLogger(
            "com.lantanagroup.link.performance." + AbstractResourceConsumer.class.getSimpleName());

    private final ResourceRepository resourceRepository;
    private final PatientReportingEvaluationStatusRepository patientStatusRepository;
    private final Map<String, PatientReportingEvaluationStatus> patientStatusCache;
    private final Predicate<MeasureReport> reportabilityPredicate;
    private final MeasureEvalMetrics measureEvalMetrics;
    private final KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
    private final EvaluateMeasureService evaluateMeasureService;
    private final PatientStatusBundler patientStatusBundler;
    private final ResourceEvaluatedProducer resourceEvaluatedProducer;

    public AbstractResourceConsumer (
            ResourceRepository resourceRepository,
            PatientReportingEvaluationStatusRepository patientStatusRepository,
            Predicate<MeasureReport> reportabilityPredicate,
            MeasureEvalMetrics measureEvalMetrics,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            EvaluateMeasureService evaluateMeasureService,
            PatientStatusBundler patientStatusBundler,
            ResourceEvaluatedProducer resourceEvaluatedProducer,
            ConsumerRecordRecoverer recoverer) {
        super(recoverer);
        this.resourceRepository = resourceRepository;
        this.patientStatusRepository = patientStatusRepository;
        patientStatusCache = Collections.synchronizedMap(new PassiveExpiringMap<>(1L, TimeUnit.MINUTES));
        this.reportabilityPredicate = reportabilityPredicate;
        this.measureEvalMetrics = measureEvalMetrics;
        this.dataAcquisitionRequestedTemplate = dataAcquisitionRequestedTemplate;
        this.evaluateMeasureService = evaluateMeasureService;
        this.patientStatusBundler = patientStatusBundler;
        this.resourceEvaluatedProducer = resourceEvaluatedProducer;
    }

    @Override
    protected void process(ConsumerRecord<String, T> record) {
        String correlationId = Headers.getCorrelationId(record.headers());

        StopWatch totalStopWatch = new StopWatch();
        StopWatch taskStopWatch = new StopWatch();
        totalStopWatch.start();

        try {
            Span currentSpan = Span.current();
            MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
            MDC.put("spanId", currentSpan.getSpanContext().getSpanId());

            taskStopWatch.start("incrementRecordCount");
            Attributes attributes = Attributes.builder().put(stringKey(DiagnosticNames.CORRELATION_ID), correlationId).build();
            measureEvalMetrics.IncrementRecordsReceivedCounter(attributes);
            taskStopWatch.stop();

            taskStopWatch.start("validateRecord");
            String facilityId = record.key();
            if (facilityId == null || facilityId.isEmpty()) {
                throw new ValidationException("Facility ID is null or empty.");
            }
            T value = record.value();
            if (value.getResource() == null && !value.isAcquisitionComplete()) {
                throw new ValidationException("Record Resource is null and AcquisitionComplete is false.");
            }
            if (value.getQueryType() == null) {
                throw new ValidationException("Query Type is null.");
            }
            if (value.getScheduledReports() == null || value.getScheduledReports().isEmpty()) {
                throw new ValidationException("Scheduled Reports is null or empty.");
            }
            if (value.getReportableEvent() == null) {
                throw new ValidationException("Reportable Event is null or empty.");
            }
            taskStopWatch.stop();

            logger.debug(
                    "Consuming {}: FACILITY=[{}] CORRELATION=[{}] RESOURCE=[{}] TAIL=[{}]",
                    KafkaUtils.format(record),
                    facilityId,
                    correlationId,
                    value.getResourceTypeAndId(),
                    value.isAcquisitionComplete());

            logger.trace("Beginning patient status update");

            PatientReportingEvaluationStatus patientStatus = patientStatusCache.computeIfAbsent(correlationId, key -> {
                taskStopWatch.start("retrieveOrCreatePatientStatus");
                PatientReportingEvaluationStatus _patientStatus = Objects.requireNonNullElseGet(
                        retrievePatientStatus(facilityId, correlationId),
                        () -> createPatientStatus(facilityId, correlationId, value));
                taskStopWatch.stop();

                return _patientStatus;
            });

            if (patientStatus.getPatientId() == null) {
                logger.trace("Setting patient status patient ID: {}", value.getPatientId());
                patientStatus.setPatientId(value.getPatientId());

                taskStopWatch.start("setPatientStatusPatientId");
                patientStatus = patientStatusRepository.setPatientId(patientStatus);
                taskStopWatch.stop();

                patientStatusCache.put(correlationId, patientStatus);
            }

            if (value.isAcquisitionComplete()) {
                logger.trace("Beginning measure evaluation");

                taskStopWatch.start("createBundle");
                Bundle bundle = patientStatusBundler.createBundle(facilityId, correlationId);
                taskStopWatch.stop();

                taskStopWatch.start("evaluateMeasures");
                evaluateMeasures(value, patientStatus, bundle);
                taskStopWatch.stop();

                return;
            }

            logger.trace("Beginning resource update");

            taskStopWatch.start("upsertResource");
            upsertResource(facilityId, correlationId, value);
            taskStopWatch.stop();
        } finally {
            totalStopWatch.stop();
            for (StopWatch.TaskInfo task : taskStopWatch.getTaskInfo()) {
                performanceLogger.trace("{}: {} ns", task.getTaskName(), task.getTimeNanos());
            }
            performanceLogger.trace("SUM_OF_TASKS: {} ns", taskStopWatch.getTotalTimeNanos());
            performanceLogger.trace("TOTAL: {} ns", totalStopWatch.getTotalTimeNanos());
        }
    }

    private Resource upsertResource (String facilityId, String correlationId, T value) {
        logger.trace("Upserting resource in database");
        Resource resource = new Resource();
        resource.setFacilityId(facilityId);
        resource.setCorrelationId(correlationId);
        resource.setPatientId(value.getPatientId());
        resource.setResourceType(value.getResourceType());
        resource.setResourceId(value.getResourceId());
        resource.setResource(value.getResource());
        return resourceRepository.upsert(resource);
    }

    private PatientReportingEvaluationStatus retrievePatientStatus (String facilityId, String correlationId) {
        logger.trace("Retrieving patient status from database");
        return patientStatusRepository.findByFacilityIdAndCorrelationId(facilityId, correlationId).orElse(null);
    }

    private PatientReportingEvaluationStatus createPatientStatus (String facilityId, String correlationId, T value) {
        logger.trace("Patient status not found; creating");
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(correlationId);
        patientStatus.setPatientId(value.getPatientId());
        patientStatus.setReportableEvent(value.getReportableEvent().toString());
        patientStatus.setReports(value.getScheduledReports().stream()
                .flatMap(scheduledReport -> Arrays.stream(scheduledReport.getReportTypes())
                        .map(reportType -> {
                            PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
                            report.setReportType(reportType);
                            report.setFrequency(scheduledReport.getFrequency());
                            report.setStartDate(scheduledReport.getStartDate());
                            report.setEndDate(scheduledReport.getEndDate());
                            report.setReportTrackingId(scheduledReport.getReportTrackingId());
                            return report;
                        })
                ).collect(Collectors.toList()));
        return patientStatusRepository.insert(patientStatus);
    }

    private void evaluateMeasures (T value, PatientReportingEvaluationStatus patientStatus, Bundle bundle) {
        logger.debug("Evaluating measures");
        for (PatientReportingEvaluationStatus.Report report : patientStatus.getReports()) {
            MeasureReport measureReport = evaluateMeasureService.evaluateMeasure(value.getQueryType().toString(), patientStatus, report, bundle);
            switch (value.getQueryType()) {
                case INITIAL -> {
                    updateReportability(patientStatus, report, measureReport);
                    resourceEvaluatedProducer.produceResourceEvaluatedRecords(value.getQueryType(), patientStatus, report, measureReport);
                }
                case SUPPLEMENTAL -> resourceEvaluatedProducer.produceResourceEvaluatedRecords(value.getQueryType(), patientStatus, report, measureReport);
                default -> throw new IllegalStateException(String.format("Unexpected query type: %s", value.getQueryType()));
            }
        }

        boolean reportablePatient = patientStatus.getReports().stream().anyMatch(PatientReportingEvaluationStatus.Report::getReportable);
        // if at least one reportable measure, increment the reportable patient counter otherwise increment the non-reportable patient counter
        updatePatientMetrics(value, patientStatus, reportablePatient);

        // if the query type is INITIAL and at least one measure is reportable, produce the DataAcquisitionRequested record
        if (value.getQueryType() == QueryType.INITIAL && reportablePatient) {
            produceDataAcquisitionRequestedRecord(value, patientStatus);
        }
    }

    private void updatePatientMetrics (T value, PatientReportingEvaluationStatus patientStatus, boolean reportablePatient) {

        if (value.getQueryType() == QueryType.INITIAL) {
            Attributes attributes = Attributes.builder().put(stringKey(DiagnosticNames.FACILITY_ID), patientStatus.getFacilityId()).
                    put(stringKey(DiagnosticNames.PATIENT_ID), patientStatus.getPatientId()).
                    put(stringKey(DiagnosticNames.CORRELATION_ID), patientStatus.getCorrelationId()).build();
            if (reportablePatient) {
                measureEvalMetrics.IncrementPatientReportableCounter(attributes);
            } else {
                measureEvalMetrics.IncrementPatientNonReportableCounter(attributes);
            }
        }
    }

    private void updateReportability (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {
        report.setReportable(reportabilityPredicate.test(measureReport));
        patientStatusRepository.save(patientStatus);
    }



    private void produceDataAcquisitionRequestedRecord (T value, PatientReportingEvaluationStatus patientStatus) {
        logger.debug("Producing {}", Topics.DATA_ACQUISITION_REQUESTED);
        DataAcquisitionRequested valueDa = new DataAcquisitionRequested();
        valueDa.setPatientId(patientStatus.getPatientId());
        valueDa.setQueryType(QueryType.SUPPLEMENTAL);
        valueDa.setReportableEvent(value.getReportableEvent().toString());
        value.getScheduledReports().forEach(scheduledReport -> {
            DataAcquisitionRequested.ScheduledReport scheduledReportDa = new DataAcquisitionRequested.ScheduledReport();
            scheduledReportDa.setReportTypes(scheduledReport.getReportTypes());
            scheduledReportDa.setStartDate(scheduledReport.getStartDate());
            scheduledReportDa.setEndDate(scheduledReport.getEndDate());
            scheduledReportDa.setFrequency(scheduledReport.getFrequency());
            scheduledReportDa.setReportTrackingId(scheduledReport.getReportTrackingId());
            valueDa.getScheduledReports().add(scheduledReportDa);
        });
        org.apache.kafka.common.header.Headers headers = new RecordHeaders().add(Headers.CORRELATION_ID, Headers.getBytes(patientStatus.getCorrelationId()));
        dataAcquisitionRequestedTemplate.send(new ProducerRecord<>(
                Topics.DATA_ACQUISITION_REQUESTED,
                null,
                patientStatus.getFacilityId(),
                valueDa,
                headers));
    }
}
