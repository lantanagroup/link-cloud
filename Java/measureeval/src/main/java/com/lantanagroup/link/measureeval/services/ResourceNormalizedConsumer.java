package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.AbstractResource;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.models.QueryResults;
import com.lantanagroup.link.measureeval.models.QueryType;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.records.ResourceNormalized;
import com.lantanagroup.link.measureeval.utils.StreamUtils;
import org.apache.commons.lang3.StringUtils;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.r4.model.ResourceType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.support.KafkaUtils;
import org.springframework.messaging.handler.annotation.Header;
import org.springframework.stereotype.Service;

import java.util.*;
import java.util.function.Predicate;
import java.util.stream.Collectors;

import static org.springframework.data.mongodb.core.query.Criteria.byExample;

@Service
public class ResourceNormalizedConsumer {
    private static final Logger logger = LoggerFactory.getLogger(ResourceNormalizedConsumer.class);

    private final MongoOperations mongoOperations;
    private final DataAcquisitionClient dataAcquisitionClient;
    private final MeasureEvaluatorCache measureEvaluatorCache;
    private final Predicate<MeasureReport> reportabilityPredicate;
    private final KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
    private final KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate;

    public ResourceNormalizedConsumer(
            MongoOperations mongoOperations,
            DataAcquisitionClient dataAcquisitionClient,
            MeasureEvaluatorCache measureEvaluatorCache,
            Predicate<MeasureReport> reportabilityPredicate,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate) {
        this.mongoOperations = mongoOperations;
        this.dataAcquisitionClient = dataAcquisitionClient;
        this.measureEvaluatorCache = measureEvaluatorCache;
        this.reportabilityPredicate = reportabilityPredicate;
        this.dataAcquisitionRequestedTemplate = dataAcquisitionRequestedTemplate;
        this.resourceEvaluatedTemplate = resourceEvaluatedTemplate;
    }

    @KafkaListener(topics = Topics.RESOURCE_NORMALIZED)
    public void consume(
            @Header(Headers.CORRELATION_ID) String correlationId,
            ConsumerRecord<String, ResourceNormalized> record) {
        String facilityId = record.key();
        ResourceNormalized value = record.value();
        logger.info(
                "Consuming record: RECORD=[{}] FACILITY=[{}] CORRELATION=[{}] RESOURCE=[{}/{}]",
                KafkaUtils.format(record), facilityId, correlationId, value.getResourceType(), value.getResourceId());

        logger.info("Beginning resource update");
        AbstractResource resource = Objects.requireNonNullElseGet(
                retrieveResource(facilityId, value),
                () -> createResource(facilityId, value));
        updateResource(value, resource);
        mongoOperations.save(resource);

        logger.info("Beginning patient status update");
        PatientReportingEvaluationStatus patientStatus = Objects.requireNonNullElseGet(
                retrievePatientStatus(facilityId, correlationId),
                () -> createPatientStatus(facilityId, correlationId, value));
        if (!patientStatus.hasQueryType(value.getQueryType())) {
            updatePatientStatus(facilityId, correlationId, value, patientStatus);
        }
        updatePatientStatusResource(value, patientStatus, NormalizationStatus.NORMALIZED);
        mongoOperations.save(patientStatus);
        if (!isReadyToEvaluate(patientStatus)) {
            logger.info("Not ready to evaluate; exiting");
            return;
        }

        logger.info("Beginning measure evaluation");
        Bundle bundle = createBundle(patientStatus);
        evaluateMeasures(value, patientStatus, bundle);
    }

    private AbstractResource retrieveResource(
            boolean isPatientResource,
            String facilityId,
            ResourceType resourceType,
            String resourceId) {
        Class<? extends AbstractResource> entityType = isPatientResource ? PatientResource.class : SharedResource.class;
        AbstractResource probe = isPatientResource ? new PatientResource() : new SharedResource();
        probe.setFacilityId(facilityId);
        probe.setResourceType(resourceType);
        probe.setResourceId(resourceId);
        return mongoOperations.query(entityType)
                .matching(byExample(probe))
                .oneValue();
    }

    private AbstractResource retrieveResource(String facilityId, ResourceNormalized value) {
        logger.debug("Retrieving resource from database");
        return retrieveResource(value.isPatientResource(), facilityId, value.getResourceType(), value.getResourceId());
    }

    private AbstractResource createResource(String facilityId, ResourceNormalized value) {
        logger.debug("Resource not found; creating");
        AbstractResource resource;
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

    private void updateResource(ResourceNormalized value, AbstractResource resource) {
        resource.setResource(value.getResource());
    }

    private PatientReportingEvaluationStatus retrievePatientStatus(String facilityId, String correlationId) {
        logger.debug("Retrieving patient status from database");
        PatientReportingEvaluationStatus probe = new PatientReportingEvaluationStatus();
        probe.setFacilityId(facilityId);
        probe.setCorrelationId(correlationId);
        probe.setReports(null);
        probe.setResources(null);
        return mongoOperations.query(PatientReportingEvaluationStatus.class)
                .matching(byExample(probe))
                .oneValue();
    }

    private PatientReportingEvaluationStatus createPatientStatus(
            String facilityId,
            String correlationId,
            ResourceNormalized value) {
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
            ResourceNormalized value,
            PatientReportingEvaluationStatus patientStatus) {
        logger.debug("Retrieving query results from data acquisition service");
        QueryResults queryResults =
                dataAcquisitionClient.getQueryResults(facilityId, correlationId, value.getQueryType());
        if (patientStatus.getPatientId() == null) {
            patientStatus.setPatientId(queryResults.getPatientId());
        }
        logger.debug("Creating patient status resources");
        queryResults.getQueryResults().stream()
                .filter(queryResult -> queryResult.getQueryType() == value.getQueryType())
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
            ResourceNormalized value,
            PatientReportingEvaluationStatus patientStatus,
            NormalizationStatus normalizationStatus) {
        PatientReportingEvaluationStatus.Resource resource = patientStatus.getResources().stream()
                .filter(_resource -> _resource.getResourceType() == value.getResourceType())
                .filter(_resource -> StringUtils.equals(_resource.getResourceId(), value.getResourceId()))
                .filter(_resource -> _resource.getQueryType() == value.getQueryType())
                .reduce(StreamUtils::toOnlyElement)
                .orElseThrow();
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
                .map(AbstractResource::getResource)
                .map(Resource.class::cast)
                .forEachOrdered(resource -> bundle.addEntry().setResource(resource));
        return bundle;
    }

    private List<AbstractResource> retrieveResources(PatientReportingEvaluationStatus patientStatus) {
        logger.debug("Retrieving resources");
        return patientStatus.getResources().stream()
                .filter(resource -> resource.getNormalizationStatus() == NormalizationStatus.NORMALIZED)
                .map(resource -> retrieveResource(patientStatus.getFacilityId(), resource))
                .toList();
    }

    private AbstractResource retrieveResource(String facilityId, PatientReportingEvaluationStatus.Resource resource) {
        logger.trace("Retrieving resource: {}/{}", resource.getResourceType(), resource.getResourceId());
        return retrieveResource(
                resource.getIsPatientResource(),
                facilityId,
                resource.getResourceType(),
                resource.getResourceId());
    }

    private void evaluateMeasures(
            ResourceNormalized value,
            PatientReportingEvaluationStatus patientStatus,
            Bundle bundle) {
        logger.debug("Evaluating measures");
        for (PatientReportingEvaluationStatus.Report report : patientStatus.getReports()) {
            MeasureReport measureReport = evaluateMeasure(patientStatus, report, bundle);
            switch (value.getQueryType()) {
                case INITIAL -> updateReportability(patientStatus, report, measureReport);
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
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {
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
        if (!measureReport.hasId()) {
            measureReport.setId(UUID.randomUUID().toString());
        }
        logger.debug("Population counts: {}", measureReport.getGroup().stream()
                .flatMap(group -> group.getPopulation().stream())
                .map(population -> String.format(
                        "%s=[%d]",
                        population.getCode().getCodingFirstRep().getCode(),
                        population.getCount()))
                .collect(Collectors.joining(" ")));
        return measureReport;
    }

    private void updateReportability(
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {
        report.setReportable(reportabilityPredicate.test(measureReport));
        mongoOperations.save(patientStatus);
    }

    private void produceResourceEvaluatedRecords(
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {
        logger.debug("Producing {} records", Topics.RESOURCE_EVALUATED);
        List<Resource> resources = measureReport.getContained();
        measureReport.setContained(null);
        produceResourceEvaluatedRecord(patientStatus, report, measureReport.getIdPart(), measureReport);
        for (Resource resource : resources) {
            produceResourceEvaluatedRecord(patientStatus, report, measureReport.getIdPart(), resource);
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