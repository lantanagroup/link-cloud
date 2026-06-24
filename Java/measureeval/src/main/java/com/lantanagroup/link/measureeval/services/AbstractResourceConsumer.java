package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.CacheType;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.QueryType;
import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.kafka.AbstractAsyncConsumer;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
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
import org.springframework.data.mongodb.core.BulkOperations;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.KafkaUtils;
import org.springframework.util.StopWatch;

import java.util.*;
import java.util.concurrent.TimeUnit;
import java.util.function.Predicate;
import java.util.stream.Collectors;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

public abstract class AbstractResourceConsumer<T extends AbstractResourceRecord> extends AbstractAsyncConsumer<ResourceKey, T> {
    private static final Logger logger = LoggerFactory.getLogger(AbstractResourceConsumer.class);
    private static final Logger performanceLogger = LoggerFactory.getLogger("com.lantanagroup.link.performance." + AbstractResourceConsumer.class.getSimpleName());

    private final PatientReportingEvaluationStatusRepository patientStatusRepository;
    private final Map<String, PatientReportingEvaluationStatus> patientStatusCache;
    private final Predicate<MeasureReport> reportabilityPredicate;
    private final MeasureEvalMetrics measureEvalMetrics;
    private final KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
    private final EvaluateMeasureService evaluateMeasureService;
    private final PatientStatusBundler patientStatusBundler;
    private final BlobStorageService blobStorageService;
    private final MeasureReportGeneratedProducer measureReportGeneratedProducer;
    private final RedisResourceService redisResourceService;
    private final AbsResourceService absResourceService;
    private final MongoOperations mongoOperations;

    public AbstractResourceConsumer (
            PatientReportingEvaluationStatusRepository patientStatusRepository,
            Predicate<MeasureReport> reportabilityPredicate,
            MeasureEvalMetrics measureEvalMetrics,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            EvaluateMeasureService evaluateMeasureService,
            PatientStatusBundler patientStatusBundler,
            BlobStorageService blobStorageService,
            ConsumerRecordRecoverer recoverer, MeasureReportGeneratedProducer measureReportGeneratedProducer,
            RedisResourceService redisResourceService,
            AbsResourceService absResourceService,
            MongoOperations mongoOperations) {
        super(recoverer);
        this.patientStatusRepository = patientStatusRepository;
        this.measureReportGeneratedProducer = measureReportGeneratedProducer;
        patientStatusCache = Collections.synchronizedMap(new PassiveExpiringMap<>(1L, TimeUnit.MINUTES));
        this.reportabilityPredicate = reportabilityPredicate;
        this.measureEvalMetrics = measureEvalMetrics;
        this.dataAcquisitionRequestedTemplate = dataAcquisitionRequestedTemplate;
        this.evaluateMeasureService = evaluateMeasureService;
        this.patientStatusBundler = patientStatusBundler;
        this.blobStorageService = blobStorageService;
        this.redisResourceService = redisResourceService;
        this.absResourceService = absResourceService;
        this.mongoOperations = mongoOperations;
    }

    @Override
    protected void process(ConsumerRecord<ResourceKey, T> record) {
        boolean perf = performanceLogger.isInfoEnabled();
        StopWatch totalStopWatch = perf ? new StopWatch() : null;
        StopWatch taskStopWatch = perf ? new StopWatch() : null;
        if (perf) totalStopWatch.start();

        try {
            Span currentSpan = Span.current();
            MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
            MDC.put("spanId", currentSpan.getSpanContext().getSpanId());

            if (perf) taskStopWatch.start("validateRecord");
            ResourceKey key = record.key();
            if (key == null || key.getFacilityId() == null || key.getFacilityId().isEmpty()) {
                throw new ValidationException("Facility ID is null or empty.");
            }
            String facilityId = key.getFacilityId();
            String patientId = key.getPatientId();
            T value = record.value();
            if (value.getQueryType() == null) {
                throw new ValidationException("Query Type is null.");
            }
            if (value.getScheduledReports() == null || value.getScheduledReports().isEmpty()) {
                throw new ValidationException("Scheduled Reports is null or empty.");
            }
            if (value.getReportableEvent() == null) {
                throw new ValidationException("Reportable Event is null or empty.");
            }
            if (value.getCacheType() == null) {
                throw new ValidationException("Cache Type is null.");
            }
            String correlationId = value.getCacheKey();
            if (correlationId == null || correlationId.isEmpty()) {
                throw new ValidationException("Cache Key is null or empty.");
            }
            if (perf) taskStopWatch.stop();

            if (perf) taskStopWatch.start("incrementRecordCount");
            Attributes attributes = Attributes.builder().put(stringKey(DiagnosticNames.CORRELATION_ID), correlationId).build();
            measureEvalMetrics.IncrementRecordsReceivedCounter(attributes);
            if (perf) taskStopWatch.stop();

            logger.debug(
                    "MESSAGE RECEIVED {}: FACILITY=[{}] PATIENT=[{}] CORRELATION=[{}] CACHE=[{}] QUERY_TYPE=[{}] REPORTS={}",
                    KafkaUtils.format(record),
                    facilityId,
                    patientId,
                    correlationId,
                    value.getCacheType(),
                    value.getQueryType(),
                    value.getScheduledReports() != null ? value.getScheduledReports().size() : 0);

            CacheType cacheType = value.getCacheType();

            if (perf) taskStopWatch.start("readResources");
            long readStart = perf ? System.nanoTime() : 0;
            List<Resource> resources;
            switch (cacheType) {
                case REDIS -> resources = redisResourceService.readResources(
                        facilityId, correlationId, patientId);
                case ABS -> {
                    if (absResourceService == null) {
                        throw new IllegalStateException("ABS cache type requested but cache-blob-storage is not configured");
                    }
                    resources = absResourceService.readResources(facilityId, correlationId, patientId, correlationId);
                }
                default -> throw new IllegalStateException("Unexpected cache type: " + cacheType);
            }
            long readMs = perf ? (System.nanoTime() - readStart) / 1_000_000 : 0;
            if (perf) taskStopWatch.stop();
            if (logger.isDebugEnabled()) {
                Map<String, Long> resourceTypeCounts = resources.stream()
                        .collect(Collectors.groupingBy(r -> r.getResourceType() != null ? r.getResourceType().name() : "Unknown", Collectors.counting()));
                logger.debug("Read {} resources from {} in {} ms for correlationId={}, resourceTypes={}",
                        resources.size(), cacheType, readMs, correlationId, resourceTypeCounts);
            }

            if (resources.isEmpty()) {
                logger.info("Cache empty for correlationId={}; evaluating with empty bundle to produce a not-reportable report", correlationId);
            }

            logger.trace("Beginning patient status update");

            PatientReportingEvaluationStatus patientStatus = patientStatusCache.computeIfAbsent(correlationId, k -> {
                if (perf) taskStopWatch.start("retrieveOrCreatePatientStatus");
                PatientReportingEvaluationStatus _patientStatus = Objects.requireNonNullElseGet(
                        retrievePatientStatus(facilityId, correlationId),
                        () -> createPatientStatus(facilityId, correlationId, patientId, value));
                if (perf) taskStopWatch.stop();

                return _patientStatus;
            });

            if (patientStatus.getPatientId() == null) {
                logger.trace("Setting patient status patient ID: {}", patientId);
                patientStatus.setPatientId(patientId);

                if (perf) taskStopWatch.start("setPatientStatusPatientId");
                patientStatus = patientStatusRepository.setPatientId(patientStatus);
                if (perf) taskStopWatch.stop();

                patientStatusCache.put(correlationId, patientStatus);
            }

            // Build the FHIR Bundle from the read resources.
            if (perf) taskStopWatch.start("createBundle");
            Bundle bundle = patientStatusBundler.createBundleFromResources(resources);
            if (perf) taskStopWatch.stop();

            // Evaluate measures and determine reportability.
            if (perf) taskStopWatch.start("evaluateMeasures");
            long kafkaIngestTimestamp = record.timestamp();
            boolean reportablePatient = evaluateMeasures(value, patientStatus, bundle, kafkaIngestTimestamp);
            if (perf) taskStopWatch.stop();

            bundle = null;

            boolean initialReportable =
                    value.getQueryType() == QueryType.INITIAL && reportablePatient;

            if (initialReportable) {
                logger.debug("Skipping Mongo write for INITIAL reportable patient, correlationId={}",
                        correlationId);
            } else {
                if (perf) taskStopWatch.start("bulkWriteToMongo");
                bulkWriteResources(resources);
                if (perf) taskStopWatch.stop();
                logger.debug("Bulk wrote {} resources to Mongo for correlationId={}",
                        resources.size(), correlationId);
            }

            // Clean up cache after SUPPLEMENTAL, or after INITIAL if patient is not reportable.
            // INITIAL + reportable keeps the cache for the SUPPLEMENTAL pass to reuse.
            if (initialReportable) {
                logger.debug("Keeping cache for SUPPLEMENTAL pass, correlationId={}", correlationId);
            } else {
                if (perf) taskStopWatch.start("cleanupCache");
                switch (cacheType) {
                    case REDIS -> redisResourceService.cleanup(correlationId);
                    case ABS -> {
                        if (absResourceService != null) {
                            absResourceService.cleanup(correlationId);
                        }
                    }
                }
                if (perf) taskStopWatch.stop();
                logger.debug("Cache cleanup complete for correlationId={}, cacheType={}", correlationId, cacheType);
            }

        } finally {
            if (perf) {
                totalStopWatch.stop();
                for (StopWatch.TaskInfo task : taskStopWatch.getTaskInfo()) {
                    performanceLogger.info("{}: {} ms", task.getTaskName(), task.getTimeNanos() / 1_000_000);
                }
                performanceLogger.info("SUM_OF_TASKS: {} ms", taskStopWatch.getTotalTimeNanos() / 1_000_000);
                performanceLogger.info("TOTAL: {} ms", totalStopWatch.getTotalTimeNanos() / 1_000_000);
            }
        }
    }

    /**
     * Bulk upsert all resources to Mongo in chunks, using deterministic _id
     * ({facilityId}:{correlationId}:{resourceType}:{resourceId})
     */
    private static final int MONGO_BULK_BATCH_SIZE = 500;
    // private static final int MONGO_DUPLICATE_KEY_CODE = 11000;

    private void bulkWriteResources(List<Resource> resources) {
        if (resources.isEmpty()) {
            return;
        }

        String correlationId = resources.get(0).getCorrelationId();
        int totalUpserted = 0;

        for (int i = 0; i < resources.size(); i += MONGO_BULK_BATCH_SIZE) {
            List<Resource> chunk = resources.subList(
                    i, Math.min(i + MONGO_BULK_BATCH_SIZE, resources.size()));

            for (Resource r : chunk) {
                if (r.getId() == null) {
                    r.setId(buildDeterministicId(r));
                }
            }

            BulkOperations bulkOps = mongoOperations.bulkOps(BulkOperations.BulkMode.UNORDERED, Resource.class);
            for (Resource r : chunk) {
                org.springframework.data.mongodb.core.query.Query query =
                        org.springframework.data.mongodb.core.query.Query.query(
                                org.springframework.data.mongodb.core.query.Criteria.where("_id").is(r.getId()));
                bulkOps.replaceOne(query, r, org.springframework.data.mongodb.core.FindAndReplaceOptions.options().upsert());
            }
            var result = bulkOps.execute();
            totalUpserted += result.getUpserts().size() + result.getModifiedCount();
        }

        logger.info("Bulk upsert complete: upserted={} total={} correlationId={}",
                totalUpserted, resources.size(), correlationId);
    }

    // private void bulkInsertResources(List<Resource> resources) {
    //     if (resources.isEmpty()) {
    //         return;
    //     }
    //
    //     String correlationId = resources.get(0).getCorrelationId();
    //     int totalInserted = 0;
    //     int totalDuplicates = 0;
    //
    //     for (int i = 0; i < resources.size(); i += MONGO_BULK_BATCH_SIZE) {
    //         List<Resource> chunk = resources.subList(
    //                 i, Math.min(i + MONGO_BULK_BATCH_SIZE, resources.size()));
    //
    //         for (Resource r : chunk) {
    //             if (r.getId() == null) {
    //                 r.setId(buildDeterministicId(r));
    //             }
    //         }
    //
    //         BulkOperations bulkOps = mongoOperations.bulkOps(BulkOperations.BulkMode.UNORDERED, Resource.class);
    //         bulkOps.insert(chunk);
    //         try {
    //             var result = bulkOps.execute();
    //             totalInserted += result.getInsertedCount();
    //         } catch (BulkOperationException e) {
    //             int dupCount = 0;
    //             List<BulkWriteError> fatal = new ArrayList<>();
    //             for (BulkWriteError err : e.getErrors()) {
    //                 if (err.getCode() == MONGO_DUPLICATE_KEY_CODE) {
    //                     dupCount++;
    //                 } else {
    //                     fatal.add(err);
    //                 }
    //             }
    //             if (!fatal.isEmpty()) {
    //                 throw e;
    //             }
    //             totalInserted += (chunk.size() - dupCount);
    //             totalDuplicates += dupCount;
    //             logger.debug("Skipped {} duplicate resource(s) on retry for correlationId={}",
    //                     dupCount, correlationId);
    //         }
    //     }
    //
    //     logger.info("Bulk insert complete: inserted={} duplicatesSkipped={} total={} correlationId={}",
    //             totalInserted, totalDuplicates, resources.size(), correlationId);
    // }

    private static String buildDeterministicId(Resource r) {
        String composite = r.getFacilityId() + ":" + r.getCorrelationId() + ":"
                + r.getResourceType().name() + ":" + r.getResourceId();
        return UUID.nameUUIDFromBytes(composite.getBytes(java.nio.charset.StandardCharsets.UTF_8)).toString();
    }

    private PatientReportingEvaluationStatus retrievePatientStatus (String facilityId, String correlationId) {
        logger.trace("Retrieving patient status from database");
        return patientStatusRepository.findByFacilityIdAndCorrelationId(facilityId, correlationId).orElse(null);
    }

    private PatientReportingEvaluationStatus createPatientStatus (String facilityId, String correlationId, String patientId, T value) {
        logger.trace("Patient status not found; creating");
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(correlationId);
        patientStatus.setPatientId(patientId);
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

    private boolean evaluateMeasures (T value, PatientReportingEvaluationStatus patientStatus, Bundle bundle, long kafkaIngestTimestamp) {
        logger.debug("Evaluating measures");

        logger.debug("EVALUATING MEASURES: FACILITY=[{}] PATIENT=[{}] CORRELATION=[{}] QUERY_TYPE=[{}] REPORT_COUNT=[{}]",
                patientStatus.getFacilityId(), patientStatus.getPatientId(), patientStatus.getCorrelationId(),
                value.getQueryType(), patientStatus.getReports().size());

        for (PatientReportingEvaluationStatus.Report report : patientStatus.getReports()) {
            logger.debug("EVALUATING REPORT: FACILITY=[{}] PATIENT=[{}] CORRELATION=[{}] REPORT_TYPE=[{}] TRACKING_ID=[{}] QUERY_TYPE=[{}] REPORTABLE=[{}]",
                    patientStatus.getFacilityId(), patientStatus.getPatientId(), patientStatus.getCorrelationId(),
                    report.getReportType(), report.getReportTrackingId(), value.getQueryType(), report.getReportable());

            //We only want to evaluate supplemental reports that have been marked as reportable. If they failed initial evaluation, then we should not perform a supplemental evaluation for the report.
            if (value.getQueryType() == QueryType.SUPPLEMENTAL && !Boolean.TRUE.equals(report.getReportable())) {
                logger.debug("SKIPPING SUPPLEMENTAL: FACILITY=[{}] PATIENT=[{}] CORRELATION=[{}] REPORT_TYPE=[{}] — not reportable",
                        patientStatus.getFacilityId(), patientStatus.getPatientId(), patientStatus.getCorrelationId(), report.getReportType());
                continue;
            }

            MeasureReport measureReport;
            if (bundle.hasEntry()) {
                measureReport = evaluateMeasureService.evaluateMeasure(value.getQueryType().toString(), patientStatus, report, bundle);
                if (measureReport.getIdPart() == null) {
                    measureReport.setId(UUID.randomUUID().toString());
                }
            } else {
                if (value.getQueryType() != QueryType.INITIAL) {
                    throw new IllegalArgumentException("Unexpected empty bundle during non-initial evaluation");
                }
                measureReport = null;
            }

            boolean reportable = false;

            switch (value.getQueryType()) {
                case INITIAL -> {
                    reportable = measureReport != null && reportabilityPredicate.test(measureReport);
                    report.setReportable(reportable);

                    if (!reportable) {
                        String measureReportId = measureReport == null ? UUID.randomUUID().toString() : measureReport.getIdPart();
                        measureReportGeneratedProducer.produceMeasureReportGeneratedRecord(patientStatus, report, measureReportId, null, null);
                        recordNormalizedToReportGeneratedDuration(kafkaIngestTimestamp, patientStatus, report);
                    }
                }
                case SUPPLEMENTAL -> {
                    blobStorageService.storePatientInBlobStorage(patientStatus, report, measureReport);
                    recordNormalizedToReportGeneratedDuration(kafkaIngestTimestamp, patientStatus, report);
                }
                default -> throw new IllegalStateException(String.format("Unexpected query type: %s", value.getQueryType()));
            }

            // if at least one reportable measure, increment the reportable patient counter otherwise increment the non-reportable patient counter
            Attributes attributes = MeasureEvalMetrics.buildAttributes(value.getQueryType().toString(), patientStatus, report.getReportTrackingId(), null);
            measureEvalMetrics.IncrementPatientReportableCounter(attributes, reportable);
        }

        if (value.getQueryType() == QueryType.INITIAL) {
            patientStatusRepository.save(patientStatus);
        }

        boolean reportablePatient = patientStatus.getReports().stream().anyMatch(r -> Boolean.TRUE.equals(r.getReportable()));
        // if at least one reportable measure, increment the reportable patient counter otherwise increment the non-reportable patient counter
        updatePatientMetrics(value, patientStatus, reportablePatient);

        // if the query type is INITIAL and at least one measure is reportable, produce the DataAcquisitionRequested record
        if (value.getQueryType() == QueryType.INITIAL && reportablePatient) {
            produceDataAcquisitionRequestedRecord(value, patientStatus);
        }

        return reportablePatient;
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

    private void recordNormalizedToReportGeneratedDuration(
            long kafkaIngestTimestamp,
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report) {
        long elapsedMs = System.currentTimeMillis() - kafkaIngestTimestamp;
        Attributes attributes = Attributes.builder()
                .put(stringKey(DiagnosticNames.FACILITY_ID), patientStatus.getFacilityId())
                .put(stringKey(DiagnosticNames.PATIENT_ID), patientStatus.getPatientId())
                .put(stringKey(DiagnosticNames.CORRELATION_ID), patientStatus.getCorrelationId())
                .put(stringKey("report.type"), report.getReportType())
                .build();
        measureEvalMetrics.recordNormalizedToReportGeneratedDuration(elapsedMs, attributes);
        logger.info("Normalized-to-MeasureReportGenerated duration: {} ms [facility={}, patient={}, correlationId={}, reportType={}]",
                elapsedMs, patientStatus.getFacilityId(), patientStatus.getPatientId(),
                patientStatus.getCorrelationId(), report.getReportType());
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
