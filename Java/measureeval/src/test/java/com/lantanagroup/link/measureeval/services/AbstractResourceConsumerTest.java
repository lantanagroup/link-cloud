package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.*;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourcesNormalized;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;

import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.ResourceType;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;
import org.springframework.data.mongodb.core.BulkOperations;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.test.util.ReflectionTestUtils;

import java.util.*;
import java.util.function.Predicate;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class AbstractResourceConsumerTest {

    @Mock private PatientReportingEvaluationStatusRepository patientStatusRepository;
    @Mock private Predicate<MeasureReport> reportabilityPredicate;
    @Mock private MeasureEvalMetrics measureEvalMetrics;
    @Mock private KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
    @Mock private EvaluateMeasureService evaluateMeasureService;
    @Mock private PatientStatusBundler patientStatusBundler;
    @Mock private BlobStorageService blobStorageService;
    @Mock private ConsumerRecordRecoverer recoverer;
    @Mock private MeasureReportGeneratedProducer measureReportGeneratedProducer;
    @Mock private RedisResourceService redisResourceService;
    @Mock private AbsResourceService absResourceService;
    @Mock private MongoOperations mongoOperations;

    private AutoCloseable mocks;
    private ResourcesNormalizedConsumer consumer;

    @BeforeEach
    void setUp() {
        mocks = MockitoAnnotations.openMocks(this);
        // Since LEGLINK-1279 ABS is the durable source and its bean is required at boot, so the
        // consumer is always wired with both stores.
        consumer = new ResourcesNormalizedConsumer(
                patientStatusRepository,
                reportabilityPredicate,
                measureEvalMetrics,
                dataAcquisitionRequestedTemplate,
                evaluateMeasureService,
                patientStatusBundler,
                blobStorageService,
                recoverer,
                measureReportGeneratedProducer,
                redisResourceService,
                absResourceService,
                mongoOperations);
    }

    @AfterEach
    void tearDown() throws Exception {
        mocks.close();
    }

    @Test
    void buildDeterministicId_returnsConsistentHash() {
        Resource r = new Resource();
        r.setFacilityId("MyFacility");
        r.setCorrelationId("corr-123");
        r.setResourceType(ResourceType.Patient);
        r.setResourceId("p-456");

        String id1 = ReflectionTestUtils.invokeMethod(consumer, "buildDeterministicId", r);
        String id2 = ReflectionTestUtils.invokeMethod(consumer, "buildDeterministicId", r);

        assertNotNull(id1);
        assertEquals(36, id1.length());
        assertEquals(id1, id2);
    }

    @Test
    void evaluateMeasures_supplementalReportable_storesInBlobStorage() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.SUPPLEMENTAL);

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(true);
        report.setReportTrackingId("tracking-1");
        report.setReportType("TestMeasure");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        ReflectionTestUtils.invokeMethod(
                consumer, "evaluateMeasures", value, patientStatus, bundle, System.currentTimeMillis());

        verify(blobStorageService).storePatientInBlobStorage(eq(patientStatus), eq(report), eq(measureReport), any());
    }


    @Test
    void evaluateMeasures_supplementalNotReportable_skipsEvaluation() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.SUPPLEMENTAL);

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(false);
        report.setReportTrackingId("tracking-1");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());

        ReflectionTestUtils.invokeMethod(
                consumer, "evaluateMeasures", value, patientStatus, bundle, System.currentTimeMillis());

        verifyNoInteractions(evaluateMeasureService);
        verifyNoInteractions(blobStorageService);
    }


    @Test
    void evaluateMeasures_initialReportable_producesDataAcquisitionRequested() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.INITIAL);
        value.setReportableEvent(ReportableEvent.ADHOC);

        TestScheduledReport sr = new TestScheduledReport();
        sr.reportTypes = new String[]{"TestMeasure"};
        sr.frequency = "Adhoc";
        sr.startDate = new Date();
        sr.endDate = new Date();
        sr.reportTrackingId = "tracking-1";
        value.setScheduledReports(List.of(sr.toScheduledReport()));

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(null);
        report.setReportTrackingId("tracking-1");
        report.setReportType("TestMeasure");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(any())).thenReturn(true);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        boolean result = Boolean.TRUE.equals(ReflectionTestUtils.invokeMethod(
                consumer, "evaluateMeasures", value, patientStatus, bundle, System.currentTimeMillis()));

        assertTrue(result);
        assertTrue(report.getReportable());
        verify(dataAcquisitionRequestedTemplate).send((ProducerRecord<String, DataAcquisitionRequested>) any());
    }

    @Test
    void evaluateMeasures_initialNotReportable_producesMeasureReportGenerated() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.INITIAL);

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(null);
        report.setReportTrackingId("tracking-1");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(any())).thenReturn(false);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        boolean result = Boolean.TRUE.equals(ReflectionTestUtils.invokeMethod(
                consumer, "evaluateMeasures", value, patientStatus, bundle, System.currentTimeMillis()));

        assertFalse(result);
        assertFalse(report.getReportable());
        verify(measureReportGeneratedProducer).produceMeasureReportGeneratedRecord(
                eq(patientStatus), eq(report), anyString(), isNull(), isNull(), any());
    }

    @Test
    void process_redisEmpty_fallsBackToAbsAndEvaluates() {
        // An absent or evicted Redis key is not a failure: ABS is the durable source, so the read
        // falls back and evaluation proceeds against the complete set (LEGLINK-1279).
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-1";

        ResourcesNormalized value = buildValue(cacheKey);

        when(redisResourceService.readResources(facilityId, cacheKey, patientId)).thenReturn(List.of());
        when(absResourceService.readResources(facilityId, cacheKey, patientId, cacheKey))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, false);
        stubMongoBulkWrite();

        consumer.process(buildConsumerRecord(facilityId, patientId, value));

        verify(redisResourceService).readResources(facilityId, cacheKey, patientId);
        verify(absResourceService).readResources(facilityId, cacheKey, patientId, cacheKey);
        verify(evaluateMeasureService).evaluateMeasure(anyString(), any(), any(), any());
    }

    @Test
    void process_redisUnavailable_fallsBackToAbsAndEvaluates() {
        // A Redis outage costs latency, never the record: the reader falls back to ABS instead of
        // letting the failure reach the retry ladder.
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-outage";

        ResourcesNormalized value = buildValue(cacheKey);

        when(redisResourceService.readResources(facilityId, cacheKey, patientId))
                .thenThrow(new ResourceCacheUnavailableException("redis down", new RuntimeException()));
        when(absResourceService.readResources(facilityId, cacheKey, patientId, cacheKey))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, false);
        stubMongoBulkWrite();

        assertDoesNotThrow(() -> consumer.process(buildConsumerRecord(facilityId, patientId, value)));

        verify(absResourceService).readResources(facilityId, cacheKey, patientId, cacheKey);
        verify(evaluateMeasureService).evaluateMeasure(anyString(), any(), any(), any());
    }

    @Test
    void process_redisHit_doesNotConsultAbs() {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-hit";

        ResourcesNormalized value = buildValue(cacheKey);

        when(redisResourceService.readResources(facilityId, cacheKey, patientId))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, false);
        stubMongoBulkWrite();

        consumer.process(buildConsumerRecord(facilityId, patientId, value));

        verify(absResourceService, never()).readResources(anyString(), anyString(), anyString(), anyString());
    }

    @Test
    void process_nullCacheType_isTolerated() {
        // The CacheType field is ignored since LEGLINK-1279 and will leave the contract; a record
        // without it must process normally (deployment-order safety for the producer-side removal).
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-no-type";

        ResourcesNormalized value = buildValue(cacheKey);
        assertNull(value.getCacheType());

        when(redisResourceService.readResources(facilityId, cacheKey, patientId))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, false);
        stubMongoBulkWrite();

        assertDoesNotThrow(() -> consumer.process(buildConsumerRecord(facilityId, patientId, value)));
    }

    @Test
    void process_supplemental_readsTheDurableSourceDirectly() {
        // The terminal read produces the submitted report and its corr hash idled through the
        // initial→supplemental gap (the prime eviction window); a key evicted there and recreated
        // by the supplemental append would read as present-but-partial in Redis. SUPPLEMENTAL
        // therefore bypasses Redis and reads ABS, which is complete by construction.
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-supp";

        ResourcesNormalized value = buildValue(cacheKey);
        value.setQueryType(QueryType.SUPPLEMENTAL);

        when(absResourceService.readResources(facilityId, cacheKey, patientId, cacheKey))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, false);
        stubMongoBulkWrite();

        consumer.process(buildConsumerRecord(facilityId, patientId, value));

        verify(absResourceService).readResources(facilityId, cacheKey, patientId, cacheKey);
        verify(redisResourceService, never()).readResources(anyString(), anyString(), anyString());
    }

    @Test
    void process_evaluationThrows_leavesCacheForRetry() {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-fail";

        ResourcesNormalized value = buildValue(cacheKey);

        when(redisResourceService.readResources(facilityId, cacheKey, patientId))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        PatientReportingEvaluationStatus patientStatus = patientStatus(facilityId, cacheKey, patientId);
        when(patientStatusRepository.findByFacilityIdAndCorrelationId(facilityId, cacheKey))
                .thenReturn(Optional.of(patientStatus));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());
        when(patientStatusBundler.createBundleFromResources(anyList())).thenReturn(bundle);

        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any()))
                .thenThrow(new RuntimeException("evaluation failed"));

        RuntimeException ex = assertThrows(RuntimeException.class,
                () -> consumer.process(buildConsumerRecord(facilityId, patientId, value)));

        assertEquals("evaluation failed", ex.getMessage());
        // A failure may be routed to -Retry: the redelivered record still needs its cached
        // resources, so process() must not clean up EITHER store. Terminal (dead-letter) cleanup
        // happens in the recoverer, which is the only place that knows the routing decision.
        verify(redisResourceService, never()).cleanup(anyString());
        verify(absResourceService, never()).cleanup(anyString());
    }

    @Test
    void process_metricsThrowsAfterValidation_leavesCacheForRetry() {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-metrics-fail";

        ResourcesNormalized value = buildValue(cacheKey);

        doThrow(new RuntimeException("metrics failed"))
                .when(measureEvalMetrics).IncrementRecordsReceivedCounter(any());

        RuntimeException ex = assertThrows(RuntimeException.class,
                () -> consumer.process(buildConsumerRecord(facilityId, patientId, value)));

        assertEquals("metrics failed", ex.getMessage());
        verify(redisResourceService, never()).cleanup(anyString());
        verify(absResourceService, never()).cleanup(anyString());
    }

    @Test
    void process_success_cleansUpBothStores() {
        // The data lives in ABS and usually also Redis, and nothing says which — cleanup must
        // clear both or the ABS blobs wait for the storage lifecycle rule.
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-success";

        ResourcesNormalized value = buildValue(cacheKey);

        when(redisResourceService.readResources(facilityId, cacheKey, patientId))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, false);
        stubMongoBulkWrite();

        consumer.process(buildConsumerRecord(facilityId, patientId, value));

        verify(redisResourceService).cleanup(cacheKey);
        verify(absResourceService).cleanup(cacheKey);
    }

    @Test
    void process_cleanupThrowsOnSuccess_doesNotFailTheRecord() {
        // Cleanup runs after the evaluation has fully succeeded; a cleanup failure must not turn a
        // processed record into a retry (which would re-evaluate and double-produce downstream) —
        // and a Redis cleanup failure must not skip the ABS cleanup.
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-cleanup-fail";

        ResourcesNormalized value = buildValue(cacheKey);

        when(redisResourceService.readResources(facilityId, cacheKey, patientId))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, false);
        stubMongoBulkWrite();

        doThrow(new RuntimeException("cleanup failed")).when(redisResourceService).cleanup(cacheKey);

        assertDoesNotThrow(() -> consumer.process(buildConsumerRecord(facilityId, patientId, value)));

        verify(redisResourceService).cleanup(cacheKey);
        verify(absResourceService).cleanup(cacheKey);
    }

    @Test
    void process_initialReportable_keepsCacheForSupplemental() {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-keep";

        ResourcesNormalized value = buildValue(cacheKey);

        when(redisResourceService.readResources(facilityId, cacheKey, patientId))
                .thenReturn(List.of(cachedResource(facilityId, cacheKey, patientId)));

        stubHappyPathEvaluation(facilityId, cacheKey, patientId, true);

        consumer.process(buildConsumerRecord(facilityId, patientId, value));

        verify(redisResourceService, never()).cleanup(anyString());
        verify(absResourceService, never()).cleanup(anyString());
        verifyNoInteractions(mongoOperations);
    }

    // ------------------------------------------------------------------ helpers

    private static org.hl7.fhir.r4.model.Patient nonEmptyPatient() {
        org.hl7.fhir.r4.model.Patient patient = new org.hl7.fhir.r4.model.Patient();
        patient.setId("patient-1");
        return patient;
    }

    private static Resource cachedResource(String facilityId, String cacheKey, String patientId) {
        Resource resource = new Resource();
        resource.setFacilityId(facilityId);
        resource.setCorrelationId(cacheKey);
        resource.setPatientId(patientId);
        resource.setResourceType(ResourceType.Patient);
        resource.setResourceId("p-1");
        resource.setResource("{}");
        return resource;
    }

    private static PatientReportingEvaluationStatus patientStatus(String facilityId, String cacheKey, String patientId) {
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(cacheKey);
        patientStatus.setPatientId(patientId);
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportType("TestMeasure");
        report.setReportTrackingId("tracking-1");
        report.setReportable(null);
        patientStatus.setReports(Collections.singletonList(report));
        return patientStatus;
    }

    private void stubHappyPathEvaluation(String facilityId, String cacheKey, String patientId, boolean reportable) {
        PatientReportingEvaluationStatus patientStatus = patientStatus(facilityId, cacheKey, patientId);
        when(patientStatusRepository.findByFacilityIdAndCorrelationId(facilityId, cacheKey))
                .thenReturn(Optional.of(patientStatus));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());
        when(patientStatusBundler.createBundleFromResources(anyList())).thenReturn(bundle);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(any())).thenReturn(reportable);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);
    }

    private void stubMongoBulkWrite() {
        BulkOperations bulkOps = mock(BulkOperations.class);
        when(mongoOperations.bulkOps(any(), eq(Resource.class))).thenReturn(bulkOps);
        com.mongodb.bulk.BulkWriteResult bulkResult = mock(com.mongodb.bulk.BulkWriteResult.class);
        when(bulkResult.getUpserts()).thenReturn(Collections.emptyList());
        when(bulkResult.getModifiedCount()).thenReturn(1);
        when(bulkOps.execute()).thenReturn(bulkResult);
    }

    private ResourcesNormalized buildValue(String cacheKey) {
        // CacheType is deliberately never set: it is ignored since LEGLINK-1279 and on its way out
        // of the contract.
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.INITIAL);
        value.setCacheKey(cacheKey);
        value.setReportableEvent(ReportableEvent.ADHOC);
        TestScheduledReport sr = new TestScheduledReport();
        sr.reportTypes = new String[]{"TestMeasure"};
        sr.frequency = "Adhoc";
        sr.startDate = new Date();
        sr.endDate = new Date();
        sr.reportTrackingId = "tracking-1";
        value.setScheduledReports(List.of(sr.toScheduledReport()));
        return value;
    }

    private ConsumerRecord<ResourceKey, ResourcesNormalized> buildConsumerRecord(
            String facilityId, String patientId, ResourcesNormalized value) {
        ResourceKey key = ResourceKey.builder().facilityId(facilityId).patientId(patientId).build();
        return new ConsumerRecord<>("ResourcesNormalized", 0, 0L, key, value);
    }

    private static class TestScheduledReport {
        String[] reportTypes;
        String frequency;
        Date startDate;
        Date endDate;
        String reportTrackingId;

        com.lantanagroup.link.measureeval.records.AbstractResourceRecord.ScheduledReport toScheduledReport() {
            var sr = new com.lantanagroup.link.measureeval.records.AbstractResourceRecord.ScheduledReport();
            sr.setReportTypes(reportTypes);
            sr.setFrequency(frequency);
            sr.setStartDate(startDate);
            sr.setEndDate(endDate);
            sr.setReportTrackingId(reportTrackingId);
            return sr;
        }
    }
}
