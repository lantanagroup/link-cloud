package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.AbstractResourceEntity;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.exceptions.ValidationException;
import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.models.QueryResults;
import com.lantanagroup.link.measureeval.models.QueryType;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.repositories.AbstractResourceRepository;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.measureeval.utils.StreamUtils;
import io.opentelemetry.api.common.Attributes;
import org.apache.commons.lang3.StringUtils;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.support.KafkaUtils;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.function.Predicate;
import java.util.stream.Collectors;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

public abstract class AbstractResourceConsumer<T extends AbstractResourceRecord> {
    private static final Logger logger = LoggerFactory.getLogger(AbstractResourceConsumer.class);
    private final AbstractResourceRepository resourceRepository;
    private final PatientReportingEvaluationStatusRepository patientStatusRepository;
    private final DataAcquisitionClient dataAcquisitionClient;
    private final MeasureEvaluatorCache measureEvaluatorCache;
    private final MeasureReportNormalizer measureReportNormalizer;
    private final Predicate<MeasureReport> reportabilityPredicate;
    private final MeasureEvalMetrics measureEvalMetrics;
    private final KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
    private final KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate;

    public AbstractResourceConsumer(
            AbstractResourceRepository resourceRepository,
            PatientReportingEvaluationStatusRepository patientStatusRepository,
            DataAcquisitionClient dataAcquisitionClient,
            MeasureEvaluatorCache measureEvaluatorCache,
            MeasureReportNormalizer measureReportNormalizer,
            Predicate<MeasureReport> reportabilityPredicate,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            @Qualifier("compressedKafkaTemplate")
            KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate,
            MeasureEvalMetrics measureEvalMetrics) {
        this.resourceRepository = resourceRepository;
        this.patientStatusRepository = patientStatusRepository;
        this.dataAcquisitionClient = dataAcquisitionClient;
        this.measureEvaluatorCache = measureEvaluatorCache;
        this.measureReportNormalizer = measureReportNormalizer;
        this.reportabilityPredicate = reportabilityPredicate;
        this.dataAcquisitionRequestedTemplate = dataAcquisitionRequestedTemplate;
        this.resourceEvaluatedTemplate = resourceEvaluatedTemplate;
        this.measureEvalMetrics = measureEvalMetrics;
    }

    protected abstract NormalizationStatus getNormalizationStatus();

    protected void doConsume(String correlationId, ConsumerRecord<String, T> record) {
        String facilityId = record.key();

        if(facilityId == null || facilityId.isEmpty()) {
            logger.error("Facility ID is null or empty. Exiting.");
            throw new ValidationException("Facility ID is null or empty.");
        }

        if(correlationId == null || correlationId.isEmpty()) {
            logger.error("Correlation ID is null or empty. Exiting.");
            throw new ValidationException("Correlation ID is null or empty.");
        }

        T value = record.value();
        logger.info(
                "Consuming record: RECORD=[{}] FACILITY=[{}] CORRELATION=[{}] RESOURCE=[{}/{}]",
                KafkaUtils.format(record), facilityId, correlationId, value.getResourceType(), value.getResourceId());

        logger.info("Beginning resource update");
        AbstractResourceEntity resource = Objects.requireNonNullElseGet(
                retrieveResource(facilityId, value),
                () -> createResource(facilityId, value));
        resource.setResource(value.getResource());
        resourceRepository.save(resource);

        logger.info("Beginning patient status update");
        PatientReportingEvaluationStatus patientStatus = Objects.requireNonNullElseGet(
                retrievePatientStatus(facilityId, correlationId),
                () -> createPatientStatus(facilityId, correlationId, value));
        if (!patientStatus.hasQueryType(value.getQueryType())) {
            updatePatientStatus(facilityId, correlationId, value, patientStatus);
        }
        updatePatientStatusResource(value, patientStatus, getNormalizationStatus());
        patientStatusRepository.save(patientStatus);
        if (!isReadyToEvaluate(patientStatus)) {
            logger.info("Not ready to evaluate; exiting");
            return;
        }

        // TODO: Prevent duplicate evaluations?
        logger.info("Beginning measure evaluation");
        Bundle bundle = createBundle(patientStatus);
        evaluateMeasures(value, patientStatus, bundle);
    }

    private AbstractResourceEntity retrieveResource(String facilityId, T value) {
        logger.debug("Retrieving resource from database");
        return resourceRepository.findOne(facilityId, value);
    }

    private AbstractResourceEntity createResource(String facilityId, T value) {
        logger.debug("Resource not found; creating");
        AbstractResourceEntity resource;
        if (value.isPatientResource()) {
            PatientResource patientResource = new PatientResource();
            patientResource.setPatientId(value.getPatientId());
            resource = patientResource;
        } else {
            resource = new SharedResource();
        }
        resource.setFacilityId(facilityId);
        resource.setResourceType(value.getResourceType());
        resource.setResourceId(value.getResourceId());
        return resource;
    }

    private PatientReportingEvaluationStatus retrievePatientStatus(String facilityId, String correlationId) {
        logger.debug("Retrieving patient status from database");
        return patientStatusRepository.findOne(facilityId, correlationId).orElse(null);
    }

    private PatientReportingEvaluationStatus createPatientStatus(String facilityId, String correlationId, T value) {
        logger.debug("Patient status not found; creating");
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(correlationId);
        patientStatus.setReports(value.getScheduledReports().stream()
                .map(scheduledReport -> {
                    PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
                    report.setReportType(scheduledReport.getReportType());
                    report.setStartDate(scheduledReport.getStartDate());
                    report.setEndDate(scheduledReport.getEndDate());
                    return report;
                })
                .toList());
        return patientStatus;
    }

    private void updatePatientStatus(
            String facilityId,
            String correlationId,
            T value,
            PatientReportingEvaluationStatus patientStatus) {
        logger.debug("Retrieving query results from Data Acquisition");
        QueryResults queryResults =
                dataAcquisitionClient.getQueryResults(facilityId, correlationId, value.getQueryType());
        if (patientStatus.getPatientId() == null) {
            patientStatus.setPatientId(queryResults.getPatientId());
        }
        logger.debug("Creating patient status resources");
        queryResults.getQueryResults().stream()
                .filter(queryResult -> queryResult.getQueryType() == value.getQueryType())
                .collect(Collectors.groupingBy(QueryResults.QueryResult::getIdElement)).entrySet().stream()
                .map(entry -> {
                    List<QueryResults.QueryResult> values = entry.getValue();
                    if (values.size() > 1) {
                        logger.warn("Multiple query results found: {}", entry.getKey());
                    }
                    return values.get(0);
                })
                .map(queryResult -> {
                    PatientReportingEvaluationStatus.Resource resource =
                            new PatientReportingEvaluationStatus.Resource();
                    resource.setResourceType(queryResult.getResourceType());
                    resource.setResourceId(queryResult.getResourceId());
                    resource.setQueryType(queryResult.getQueryType());
                    resource.setNormalizationStatus(NormalizationStatus.PENDING);
                    return resource;
                })
                .forEachOrdered(patientStatus.getResources()::add);
    }

    private void updatePatientStatusResource(
            T value,
            PatientReportingEvaluationStatus patientStatus,
            NormalizationStatus normalizationStatus) {
        PatientReportingEvaluationStatus.Resource resource = patientStatus.getResources().stream()
                .filter(_resource -> _resource.getResourceType() == value.getResourceType())
                .filter(_resource -> StringUtils.equals(_resource.getResourceId(), value.getResourceId()))
                .filter(_resource -> _resource.getQueryType() == value.getQueryType())
                .reduce(StreamUtils::toOnlyElement)
                .orElseGet(() -> {
                    logger.warn(
                            "No patient status resource found: {}/{}",
                            value.getResourceType(), value.getResourceId());
                    return null;
                });
        if (resource == null) {
            return;
        }
        resource.setIsPatientResource(value.isPatientResource());
        resource.setNormalizationStatus(normalizationStatus);
    }

    private boolean isReadyToEvaluate(PatientReportingEvaluationStatus patientStatus) {
        Map<NormalizationStatus, Long> countsByNormalizationStatus = new LinkedHashMap<>();
        for (NormalizationStatus normalizationStatus : NormalizationStatus.values()) {
            countsByNormalizationStatus.put(normalizationStatus, 0L);
        }
        for (PatientReportingEvaluationStatus.Resource resource : patientStatus.getResources()) {
            countsByNormalizationStatus.merge(resource.getNormalizationStatus(), 1L, Long::sum);
        }
        logger.debug("Resource counts: {}", countsByNormalizationStatus.entrySet().stream()
                .map(entry -> String.format("%s=[%d]", entry.getKey(), entry.getValue()))
                .collect(Collectors.joining(" ")));
        return countsByNormalizationStatus.get(NormalizationStatus.PENDING) == 0L;
    }

    private Bundle createBundle(PatientReportingEvaluationStatus patientStatus) {
        logger.debug("Creating bundle");
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        retrieveResources(patientStatus).stream()
                .map(AbstractResourceEntity::getResource)
                .map(Resource.class::cast)
                .forEachOrdered(resource -> bundle.addEntry().setResource(resource));
        return bundle;
    }

    private List<AbstractResourceEntity> retrieveResources(PatientReportingEvaluationStatus patientStatus) {
        logger.debug("Retrieving resources");
        return patientStatus.getResources().stream()
                .filter(resource -> resource.getNormalizationStatus() == NormalizationStatus.NORMALIZED)
                .map(resource -> retrieveResource(patientStatus.getFacilityId(), resource))
                .toList();
    }

    private AbstractResourceEntity retrieveResource(
            String facilityId,
            PatientReportingEvaluationStatus.Resource resource) {
        logger.trace("Retrieving resource: {}/{}", resource.getResourceType(), resource.getResourceId());
        return resourceRepository.findOne(facilityId, resource);
    }

    private void evaluateMeasures(T value, PatientReportingEvaluationStatus patientStatus, Bundle bundle) {
        logger.debug("Evaluating measures");
        for (PatientReportingEvaluationStatus.Report report : patientStatus.getReports()) {
            MeasureReport measureReport = evaluateMeasure(value.getQueryType().toString(), patientStatus, report, bundle);
            switch (value.getQueryType()) {
                case INITIAL -> {
                    updateReportability(patientStatus, report, measureReport);

                    if(!report.getReportable())
                    {
                        produceResourceEvaluatedRecords(patientStatus, report, measureReport);
                    }
                }
                case SUPPLEMENTAL -> produceResourceEvaluatedRecords(patientStatus, report, measureReport);
                default -> throw new IllegalStateException(
                        String.format("Unexpected query type: %s", value.getQueryType()));
            }
        }
        boolean reportable = patientStatus.getReports().stream()
                .anyMatch(PatientReportingEvaluationStatus.Report::getReportable);

        if (value.getQueryType() == QueryType.INITIAL && reportable) {
            produceDataAcquisitionRequestedRecord(patientStatus);
        }
    }

    private MeasureReport evaluateMeasure(
            String queryType,
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {

        long start = System.currentTimeMillis();

        String measureId = report.getReportType();
        logger.debug("Evaluating measure: {}", measureId);
        MeasureEvaluator measureEvaluator = measureEvaluatorCache.get(measureId);
        if (measureEvaluator == null) {
            throw new IllegalStateException(String.format("Unknown measure: %s", measureId));
        }
        MeasureReport measureReport = measureEvaluator.evaluate(
                report.getStartDate(),
                report.getEndDate(),
                patientStatus.getPatientId(),
                bundle);
        logger.debug("Population counts: {}", measureReport.getGroup().stream()
                .flatMap(group -> group.getPopulation().stream())
                .map(population -> String.format(
                        "%s=[%d]",
                        population.getCode().getCodingFirstRep().getCode(),
                        population.getCount()))
                .collect(Collectors.joining(" ")));

        long timeElapsed = System.currentTimeMillis() - start;
        Attributes attributes = Attributes.builder().put(stringKey("facilityId"), patientStatus.getFacilityId()).
                put(stringKey("patientId"), patientStatus.getPatientId()).
                put(stringKey("reportType"), report.getReportType()).
                put(stringKey("startDate"), report.getStartDate().toString()).
                put(stringKey("endDate"), report.getEndDate().toString()).
                put(stringKey("queryType"), queryType).
                put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();
        logger.info("Measure evaluation duration for Patient {}: {}", patientStatus.getPatientId(), timeElapsed);
        // Record the duration of the evaluation
        measureEvalMetrics.MeasureEvalDuration(timeElapsed, attributes );

        // count the number of patients evaluated (reportable or non-reportable)
        measureEvalMetrics.IncrementPatientEvaluatedCounter(attributes);

        return measureReport;
    }

    private void updateReportability(
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {
        report.setReportable(reportabilityPredicate.test(measureReport));
        patientStatusRepository.save(patientStatus);
    }

    private void produceResourceEvaluatedRecords(
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {
        logger.debug("Producing {} records", Topics.RESOURCE_EVALUATED);

        Attributes attributes = Attributes.builder().put(stringKey("facilityId"), patientStatus.getFacilityId()).
                put(stringKey("patientId"), patientStatus.getPatientId()).
                put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();

        var list = measureReportNormalizer.normalize(measureReport);

        if(!report.getReportable())
        {
            var measure = list.stream().filter(h -> h instanceof MeasureReport).findFirst().get();
            produceResourceEvaluatedRecord(patientStatus, report, measureReport.getIdPart(), measure);

            // Count non-reportable patients
            measureEvalMetrics.IncrementPatientNonReportableCounter(attributes);
        }
        else {
            for (Resource resource : list) {
                produceResourceEvaluatedRecord(patientStatus, report, measureReport.getIdPart(), resource);
            }
            // Count reportable patients
            measureEvalMetrics.IncrementPatientReportableCounter(attributes);
        }
    }

    private void produceResourceEvaluatedRecord(
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            String measureReportId,
            Resource resource) {
        logger.trace(
                "Producing {} record: {}/{}",
                Topics.RESOURCE_EVALUATED, resource.getResourceType(), resource.getIdPart());
        ResourceEvaluated.Key key = new ResourceEvaluated.Key();
        key.setFacilityId(patientStatus.getFacilityId());
        key.setReportType(report.getReportType());
        key.setStartDate(report.getStartDate());
        key.setEndDate(report.getEndDate());
        ResourceEvaluated value = new ResourceEvaluated();
        value.setMeasureReportId(measureReportId);
        value.setPatientId(patientStatus.getPatientId());
        value.setResource(resource);
        value.setIsReportable(report.getReportable());

        org.apache.kafka.common.header.Headers headers = new RecordHeaders()
                .add(Headers.CORRELATION_ID, Headers.getBytes(patientStatus.getCorrelationId()));

        resourceEvaluatedTemplate.send(new ProducerRecord<>(
                Topics.RESOURCE_EVALUATED,
                null,
                key,
                value,
                headers));
    }

    private void produceDataAcquisitionRequestedRecord(PatientReportingEvaluationStatus patientStatus) {
        logger.debug("Producing {} record", Topics.DATA_ACQUISITION_REQUESTED);
        DataAcquisitionRequested value = new DataAcquisitionRequested();
        value.setPatientId(patientStatus.getPatientId());
        value.setQueryType(QueryType.SUPPLEMENTAL);
        patientStatus.getReports().stream()
                .filter(PatientReportingEvaluationStatus.Report::getReportable)
                .map(report -> {
                    DataAcquisitionRequested.ScheduledReport scheduledReport =
                            new DataAcquisitionRequested.ScheduledReport();
                    scheduledReport.setReportType(report.getReportType());
                    scheduledReport.setStartDate(report.getStartDate());
                    scheduledReport.setEndDate(report.getEndDate());
                    return scheduledReport;
                })
                .forEachOrdered(value.getScheduledReports()::add);
        org.apache.kafka.common.header.Headers headers = new RecordHeaders()
                .add(Headers.CORRELATION_ID, Headers.getBytes(patientStatus.getCorrelationId()));
        dataAcquisitionRequestedTemplate.send(new ProducerRecord<>(
                Topics.DATA_ACQUISITION_REQUESTED,
                null,
                patientStatus.getFacilityId(),
                value,
                headers));
    }
}
