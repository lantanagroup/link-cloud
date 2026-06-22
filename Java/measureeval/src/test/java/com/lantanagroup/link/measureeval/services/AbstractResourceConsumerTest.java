package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.*;
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
    private ResourcesNormalizedConsumer consumerWithAbs;

    @BeforeEach
    void setUp() {
        mocks = MockitoAnnotations.openMocks(this);
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
                null,
                mongoOperations);
        consumerWithAbs = new ResourcesNormalizedConsumer(
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

        verify(blobStorageService).storePatientInBlobStorage(eq(patientStatus), eq(report), eq(measureReport));
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
                eq(patientStatus), eq(report), anyString(), isNull(), isNull());
    }

    @Test
    void process_absCacheType_readsFromAbsAndEvaluates() throws Exception {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-1";

        ResourcesNormalized value = buildAbsValue(cacheKey);

        Resource resource = new Resource();
        resource.setFacilityId(facilityId);
        resource.setCorrelationId(cacheKey);
        resource.setPatientId(patientId);
        resource.setResourceType(ResourceType.Patient);
        resource.setResourceId("p-1");
        resource.setResource("{}");

        when(absResourceService.readResources(facilityId, cacheKey, patientId, cacheKey))
                .thenReturn(List.of(resource));

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(cacheKey);
        patientStatus.setPatientId(patientId);
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportType("TestMeasure");
        report.setReportTrackingId("tracking-1");
        report.setReportable(null);
        patientStatus.setReports(Collections.singletonList(report));
        when(patientStatusRepository.findByFacilityIdAndCorrelationId(facilityId, cacheKey))
                .thenReturn(Optional.of(patientStatus));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());
        when(patientStatusBundler.createBundleFromResources(anyList())).thenReturn(bundle);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(any())).thenReturn(false);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        BulkOperations bulkOps = mock(BulkOperations.class);
        when(mongoOperations.bulkOps(any(), eq(Resource.class))).thenReturn(bulkOps);
        com.mongodb.bulk.BulkWriteResult bulkResult = mock(com.mongodb.bulk.BulkWriteResult.class);
        when(bulkResult.getUpserts()).thenReturn(Collections.emptyList());
        when(bulkResult.getModifiedCount()).thenReturn(1);
        when(bulkOps.execute()).thenReturn(bulkResult);

        ConsumerRecord<ResourceKey, ResourcesNormalized> record = buildConsumerRecord(facilityId, patientId, value);
        consumerWithAbs.process(record);

        verify(absResourceService).readResources(facilityId, cacheKey, patientId, cacheKey);
        verifyNoInteractions(redisResourceService);
        verify(evaluateMeasureService).evaluateMeasure(anyString(), any(), any(), any());
    }

    @Test
    void process_absCacheType_notConfigured_throwsIllegalStateException() {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-1";

        ResourcesNormalized value = buildAbsValue(cacheKey);
        ConsumerRecord<ResourceKey, ResourcesNormalized> record = buildConsumerRecord(facilityId, patientId, value);

        IllegalStateException ex = assertThrows(IllegalStateException.class, () -> consumer.process(record));
        assertEquals("ABS cache type requested but cache-blob-storage is not configured", ex.getMessage());
    }

    @Test
    void process_absCacheType_doesNotCleanupRedis() throws Exception {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-3";

        ResourcesNormalized value = buildAbsValue(cacheKey);

        Resource resource = new Resource();
        resource.setFacilityId(facilityId);
        resource.setCorrelationId(cacheKey);
        resource.setPatientId(patientId);
        resource.setResourceType(ResourceType.Patient);
        resource.setResourceId("p-1");
        resource.setResource("{}");

        when(absResourceService.readResources(facilityId, cacheKey, patientId, cacheKey))
                .thenReturn(List.of(resource));

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(cacheKey);
        patientStatus.setPatientId(patientId);
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportType("TestMeasure");
        report.setReportTrackingId("tracking-1");
        report.setReportable(null);
        patientStatus.setReports(Collections.singletonList(report));
        when(patientStatusRepository.findByFacilityIdAndCorrelationId(facilityId, cacheKey))
                .thenReturn(Optional.of(patientStatus));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());
        when(patientStatusBundler.createBundleFromResources(anyList())).thenReturn(bundle);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(any())).thenReturn(false);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        BulkOperations bulkOps = mock(BulkOperations.class);
        when(mongoOperations.bulkOps(any(), eq(Resource.class))).thenReturn(bulkOps);
        com.mongodb.bulk.BulkWriteResult bulkResult = mock(com.mongodb.bulk.BulkWriteResult.class);
        when(bulkResult.getUpserts()).thenReturn(Collections.emptyList());
        when(bulkResult.getModifiedCount()).thenReturn(1);
        when(bulkOps.execute()).thenReturn(bulkResult);

        ConsumerRecord<ResourceKey, ResourcesNormalized> record = buildConsumerRecord(facilityId, patientId, value);
        consumerWithAbs.process(record);

        verify(redisResourceService, never()).cleanup(anyString());
    }

    /**
     * Anchors the non-idempotent-replay defect: process() emits MeasureReportGenerated DURING
     * evaluation, before the Mongo bulk write. When the Mongo write fails (e.g. Cosmos RU
     * throttling), each delivery attempt re-runs process() and re-emits the downstream event.
     * <p>
     * Asserts the desired invariant — the event is emitted at most once regardless of how many
     * attempts occur. This currently FAILS ("wanted at most 1 time but was 2 times"), proving the
     * duplicate emission. It should pass once produces are moved after all fallible persistence.
     */
    @Test
    void process_failsAtMongoWrite_doesNotDuplicateDownstreamEventAcrossRetries() throws Exception {
        String facilityId = "facility-1";
        String patientId = "patient-1";
        String cacheKey = "cache-key-dup";

        ResourcesNormalized value = buildAbsValue(cacheKey); // INITIAL

        Resource resource = new Resource();
        resource.setFacilityId(facilityId);
        resource.setCorrelationId(cacheKey);
        resource.setPatientId(patientId);
        resource.setResourceType(ResourceType.Patient);
        resource.setResourceId("p-1");
        resource.setResource("{}");
        when(absResourceService.readResources(facilityId, cacheKey, patientId, cacheKey))
                .thenReturn(List.of(resource));

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(cacheKey);
        patientStatus.setPatientId(patientId);
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportType("TestMeasure");
        report.setReportTrackingId("tracking-1");
        report.setReportable(null);
        patientStatus.setReports(Collections.singletonList(report));
        when(patientStatusRepository.findByFacilityIdAndCorrelationId(facilityId, cacheKey))
                .thenReturn(Optional.of(patientStatus));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(nonEmptyPatient());
        when(patientStatusBundler.createBundleFromResources(anyList())).thenReturn(bundle);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        // Non-reportable INITIAL -> emits MeasureReportGenerated, then proceeds to the Mongo write.
        when(reportabilityPredicate.test(any())).thenReturn(false);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        // Transient persistence failure AFTER the event was already emitted.
        BulkOperations bulkOps = mock(BulkOperations.class);
        when(mongoOperations.bulkOps(any(), eq(Resource.class))).thenReturn(bulkOps);
        when(bulkOps.execute()).thenThrow(new RuntimeException("Mongo throttled (simulated RU limit)"));

        ConsumerRecord<ResourceKey, ResourcesNormalized> record = buildConsumerRecord(facilityId, patientId, value);

        // Original delivery + one retry, each failing at the Mongo write.
        assertThrows(RuntimeException.class, () -> consumerWithAbs.process(record));
        assertThrows(RuntimeException.class, () -> consumerWithAbs.process(record));

        // The downstream event must not be emitted once per attempt.
        verify(measureReportGeneratedProducer, atMost(1)).produceMeasureReportGeneratedRecord(
                eq(patientStatus), eq(report), anyString(), isNull(), isNull());
    }

    private static org.hl7.fhir.r4.model.Patient nonEmptyPatient() {
        org.hl7.fhir.r4.model.Patient patient = new org.hl7.fhir.r4.model.Patient();
        patient.setId("patient-1");
        return patient;
    }

    private ResourcesNormalized buildAbsValue(String cacheKey) {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.INITIAL);
        value.setCacheType(CacheType.ABS);
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
