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
import com.lantanagroup.link.measureeval.records.*;
import com.lantanagroup.link.measureeval.utils.StreamUtils;
import lombok.Getter;
import org.apache.commons.lang3.StringUtils;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.r4.model.ResourceType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.core.KafkaTemplate;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.function.Predicate;
import java.util.stream.Collectors;

import static org.springframework.data.mongodb.core.query.Criteria.byExample;


@Getter
public class BaseConsumer<T extends BaseResource> {

  private static final Logger logger = LoggerFactory.getLogger(BaseConsumer.class);
  private final MongoOperations mongoOperations;
  private final DataAcquisitionClient dataAcquisitionClient;
  private final MeasureEvaluatorCache measureEvaluatorCache;
  private final Predicate<MeasureReport> reportabilityPredicate;
  private final KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
  private final KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate;

  public BaseConsumer (
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

  private MeasureReport evaluateMeasure (
          PatientReportingEvaluationStatus patientStatus,
          PatientReportingEvaluationStatus.Report report,
          Bundle bundle) {
    String measureId = report.getReportType();
    logger.debug("Evaluating measure: {}", measureId);
    MeasureEvaluator measureEvaluator = getMeasureEvaluatorCache().get(measureId);
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

  private void updateReportability (
          PatientReportingEvaluationStatus patientStatus,
          PatientReportingEvaluationStatus.Report report,
          MeasureReport measureReport) {
    report.setReportable(getReportabilityPredicate().test(measureReport));
    getMongoOperations().save(patientStatus);
  }

  private void produceResourceEvaluatedRecords (
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

  private void produceResourceEvaluatedRecord (
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
    getResourceEvaluatedTemplate().send(new ProducerRecord<>(
            Topics.RESOURCE_EVALUATED,
            null,
            key,
            value,
            headers));
  }

  private void produceDataAcquisitionRequestedRecord (PatientReportingEvaluationStatus patientStatus) {
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
    getDataAcquisitionRequestedTemplate().send(new ProducerRecord<>(
            Topics.DATA_ACQUISITION_REQUESTED,
            null,
            patientStatus.getFacilityId(),
            value,
            headers));
  }

  protected void updatePatientStatus (
          String facilityId,
          String correlationId,
          T value,
          PatientReportingEvaluationStatus patientStatus) {
    logger.debug("Retrieving query results from data acquisition service");
    QueryResults queryResults =
            getDataAcquisitionClient().getQueryResults(facilityId, correlationId, value.getQueryType());
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

  protected void updatePatientStatusResource (
          T value,
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

  protected boolean isReadyToEvaluate (PatientReportingEvaluationStatus patientStatus) {
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

  protected Bundle createBundle (PatientReportingEvaluationStatus patientStatus) {
    logger.debug("Creating bundle");
    Bundle bundle = new Bundle();
    bundle.setType(Bundle.BundleType.COLLECTION);
    retrieveResources(patientStatus).stream()
            .map(AbstractResource::getResource)
            .map(Resource.class::cast)
            .forEachOrdered(resource -> bundle.addEntry().setResource(resource));
    return bundle;
  }

  protected void updateResource (T value, AbstractResource resource) {
    resource.setResource(value.getResource());
  }

  private List<AbstractResource> retrieveResources (PatientReportingEvaluationStatus patientStatus) {
    logger.debug("Retrieving resources");
    return patientStatus.getResources().stream()
            .filter(resource -> resource.getNormalizationStatus() == NormalizationStatus.NORMALIZED)
            .map(resource -> retrieveResource(patientStatus.getFacilityId(), resource))
            .toList();
  }

  protected AbstractResource retrieveResource (String facilityId, PatientReportingEvaluationStatus.Resource resource) {
    logger.trace("Retrieving resource: {}/{}", resource.getResourceType(), resource.getResourceId());
    return retrieveResource(
            resource.getIsPatientResource(),
            facilityId,
            resource.getResourceType(),
            resource.getResourceId());
  }

  protected AbstractResource retrieveResource (String facilityId, T value) {
    logger.debug("Retrieving resource from database");
    return retrieveResource(value.isPatientResource(), facilityId, value.getResourceType(), value.getResourceId());
  }


  protected AbstractResource retrieveResource (
          boolean isPatientResource,
          String facilityId,
          ResourceType resourceType,
          String resourceId) {
    Class<? extends AbstractResource> entityType = isPatientResource ? PatientResource.class : SharedResource.class;
    AbstractResource probe = isPatientResource ? new PatientResource() : new SharedResource();
    probe.setFacilityId(facilityId);
    probe.setResourceType(resourceType);
    probe.setResourceId(resourceId);
    return getMongoOperations().query(entityType)
            .matching(byExample(probe))
            .oneValue();
  }

  protected AbstractResource createResource (String facilityId, T value) {
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


  protected PatientReportingEvaluationStatus createPatientStatus (
          String facilityId,
          String correlationId,
          T value) {
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

  protected PatientReportingEvaluationStatus retrievePatientStatus (String facilityId, String correlationId) {
    logger.debug("Retrieving patient status from database");
    PatientReportingEvaluationStatus probe = new PatientReportingEvaluationStatus();
    probe.setFacilityId(facilityId);
    probe.setCorrelationId(correlationId);
    probe.setReports(null);
    probe.setResources(null);
    return getMongoOperations().query(PatientReportingEvaluationStatus.class)
            .matching(byExample(probe))
            .oneValue();
  }

  protected void evaluateMeasures (
          T value,
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
}
