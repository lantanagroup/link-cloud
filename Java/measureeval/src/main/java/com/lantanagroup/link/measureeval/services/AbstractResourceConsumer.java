package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.AbstractResourceEntity;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.exceptions.ValidationException;
import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.models.QueryType;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.repositories.AbstractResourceRepository;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.trace.Span;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.support.KafkaUtils;

import java.util.*;
import java.util.function.Predicate;
import java.util.stream.Collectors;

import static io.opentelemetry.api.common.AttributeKey.stringKey;

public abstract class AbstractResourceConsumer<T extends AbstractResourceRecord> {
    private static final Logger logger = LoggerFactory.getLogger(AbstractResourceConsumer.class);
    private final AbstractResourceRepository resourceRepository;
    private final PatientReportingEvaluationStatusRepository patientStatusRepository;
    private final MeasureEvaluatorCache measureEvaluatorCache;
    private final MeasureReportNormalizer measureReportNormalizer;
    private final Predicate<MeasureReport> reportabilityPredicate;
    private final MeasureEvalMetrics measureEvalMetrics;
    private final KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
    @Qualifier("compressedKafkaTemplate")
    private final KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate;

    protected abstract NormalizationStatus getNormalizationStatus ();

    public AbstractResourceConsumer (
            AbstractResourceRepository resourceRepository,
            PatientReportingEvaluationStatusRepository patientStatusRepository,
            MeasureEvaluatorCache measureEvaluatorCache,
            MeasureReportNormalizer measureReportNormalizer,
            Predicate<MeasureReport> reportabilityPredicate,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            @Qualifier("compressedKafkaTemplate")
            KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate,
            MeasureEvalMetrics measureEvalMetrics) {
        this.resourceRepository = resourceRepository;
        this.patientStatusRepository = patientStatusRepository;
        this.measureEvaluatorCache = measureEvaluatorCache;
        this.measureReportNormalizer = measureReportNormalizer;
        this.reportabilityPredicate = reportabilityPredicate;
        this.dataAcquisitionRequestedTemplate = dataAcquisitionRequestedTemplate;
        this.resourceEvaluatedTemplate = resourceEvaluatedTemplate;
        this.measureEvalMetrics = measureEvalMetrics;
    }

    protected void doConsume (String correlationId, ConsumerRecord<String, T> record) {

        Span currentSpan = Span.current();
        MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
        MDC.put("spanId", currentSpan.getSpanContext().getSpanId());

        Attributes attributes = Attributes.builder().put(stringKey("correlationId"), correlationId).build();
        measureEvalMetrics.IncrementRecordsReceivedCounter(attributes);

        String facilityId = record.key();

        if (facilityId == null || facilityId.isEmpty()) {
            logger.error("Facility ID is null or empty. Exiting.");
            throw new ValidationException("Facility ID is null or empty.");
        }

        T value = record.value();

        if (value.getResource() == null && !value.isAcquisitionComplete()) {
            logger.error("Record Resource is null and AcquisitionComplete is false. Exiting.");
            throw new ValidationException("Record Resource is null and AcquisitionComplete is false.");
        }
        if (value.getQueryType() == null) {
            logger.error("Query Type is null. Exiting.");
            throw new ValidationException("Query Type is null.");
        }
        if (value.getScheduledReports() == null || value.getScheduledReports().isEmpty()) {
            logger.error("Scheduled Reports is null or empty. Exiting.");
            throw new ValidationException("Scheduled Reports is null or empty.");
        }
        if (value.getReportableEvent() == null) {
            logger.error("Reportable Event is null or empty. Exiting.");
            throw new ValidationException("Reportable Event is null or empty.");
        }

        if (value.isAcquisitionComplete()) {
            if (logger.isInfoEnabled()) {
                logger.info("Consuming record: RECORD=[{}] FACILITY=[{}] CORRELATION=[{}] ACQUISITION COMPLETE=[{}]", KafkaUtils.format(record), facilityId, correlationId, value.isAcquisitionComplete());
            }
            PatientReportingEvaluationStatus patientStatus = Objects.requireNonNullElseGet(retrievePatientStatus(facilityId, correlationId), () -> createPatientStatus(facilityId, correlationId, value));
            Bundle bundle = createBundle(patientStatus);
            evaluateMeasures(value, patientStatus, bundle);
            return;
        }

        if (logger.isInfoEnabled()) {
            logger.info("Consuming record: RECORD=[{}] FACILITY=[{}] CORRELATION=[{}] RESOURCE=[{}/{}] ACQUISITION COMPLETE=[{}]", KafkaUtils.format(record), facilityId, correlationId, value.getResourceType(), value.getResourceId(), value.isAcquisitionComplete());
            logger.info("Beginning resource update");
        }

        AbstractResourceEntity resource = Objects.requireNonNullElseGet(retrieveResource(facilityId, value), () -> createResource(facilityId, value));
        resource.setResource(value.getResource());
        resourceRepository.save(resource);

        if (logger.isInfoEnabled()) {
            logger.info("Beginning patient status update");
        }
        PatientReportingEvaluationStatus patientStatus = Objects.requireNonNullElseGet(retrievePatientStatus(facilityId, correlationId), () -> createPatientStatus(facilityId, correlationId, value));

        if (patientStatus.getPatientId() == null) {
            if (logger.isDebugEnabled()) {
                logger.debug("Patient Id is set to : {}", value.getPatientId());
            }
            patientStatus.setPatientId(value.getPatientId());
        }

        // add the resource in the PatientReportingEvaluationStatus only if does not exist already
        PatientReportingEvaluationStatus.Resource existingResource = patientStatus.getResources() == null ? null : patientStatus.getResources().stream().filter(res -> res.getResourceType() == value.getResourceType() && Objects.equals(res.getResourceId(), value.getResourceId())).findFirst().orElse(null);

        if (existingResource == null) {
            PatientReportingEvaluationStatus.Resource statusResource = new PatientReportingEvaluationStatus.Resource();
            statusResource.setResourceType(value.getResourceType());
            statusResource.setResourceId(value.getResourceId());
            statusResource.setQueryType(value.getQueryType());
            statusResource.setIsPatientResource(value.isPatientResource());
            statusResource.setNormalizationStatus(getNormalizationStatus());
            patientStatus.getResources().add(statusResource);
            patientStatusRepository.save(patientStatus);
        }
        else {
            String resourceKey =  value.getResourceType() + ":" + value.getResourceId();
            logger.error("Duplicate resource: {}", resourceKey);
            throw new ValidationException("Duplicate resource: " + resourceKey);
        }
    }

    private AbstractResourceEntity retrieveResource (String facilityId, T value) {
        if (logger.isDebugEnabled()) {
            logger.debug("Retrieving resource from database");
        }
        return resourceRepository.findOne(facilityId, value);
    }

    private AbstractResourceEntity createResource (String facilityId, T value) {
        if (logger.isDebugEnabled()) {
            logger.debug("Resource not found; creating");
        }
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

    private PatientReportingEvaluationStatus retrievePatientStatus (String facilityId, String correlationId) {
        if (logger.isDebugEnabled()) {
            logger.debug("Retrieving patient status from database");
        }
        return patientStatusRepository.findOne(facilityId, correlationId).orElse(null);
    }

    private PatientReportingEvaluationStatus createPatientStatus (String facilityId, String correlationId, T value) {
        if (logger.isDebugEnabled()) {
            logger.debug("Patient status not found; creating");
        }
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId(facilityId);
        patientStatus.setCorrelationId(correlationId);
        patientStatus.setReportableEvent(value.getReportableEvent().toString());
        patientStatus.setReports(value.getScheduledReports().stream()
                .flatMap(scheduledReport -> Arrays.stream(scheduledReport.getReportTypes())
                        .map(reportType -> {
                            PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
                            report.setReportType(reportType);
                            report.setFrequency(scheduledReport.getFrequency());
                            report.setStartDate(scheduledReport.getStartDate());
                            report.setEndDate(scheduledReport.getEndDate());
                            return report;
                        })
                ).collect(Collectors.toList()));
        return patientStatus;
    }

    private Bundle createBundle (PatientReportingEvaluationStatus patientStatus) {
        if (logger.isDebugEnabled()) {
            logger.debug("Creating bundle");
        }
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        retrieveResources(patientStatus).stream()
                .map(AbstractResourceEntity::getResource)
                .map(Resource.class::cast)
                .forEachOrdered(resource -> bundle.addEntry().setResource(resource));
        return bundle;
    }

    private List<AbstractResourceEntity> retrieveResources (PatientReportingEvaluationStatus patientStatus) {
        if (logger.isDebugEnabled()) {
            logger.debug("Retrieving resources");
        }

        Set<Map.Entry<String, String>> singles = new HashSet<>();

        return patientStatus.getResources().stream()
                .filter(resource -> resource.getNormalizationStatus() == NormalizationStatus.NORMALIZED)
                .filter(resource -> singles.add(new AbstractMap.SimpleEntry<>(resource.getResourceType().toString(), resource.getResourceId())))
                .map(resource -> retrieveResource(patientStatus.getFacilityId(), resource))
                .toList();
    }

    private AbstractResourceEntity retrieveResource (
            String facilityId,
            PatientReportingEvaluationStatus.Resource resource) {
        logger.trace("Retrieving resource: {}/{}", resource.getResourceType(), resource.getResourceId());
        return resourceRepository.findOne(facilityId, resource);
    }

    private void evaluateMeasures (T value, PatientReportingEvaluationStatus patientStatus, Bundle bundle) {
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measures");
        }
        for (PatientReportingEvaluationStatus.Report report : patientStatus.getReports()) {
            MeasureReport measureReport = evaluateMeasure(value.getQueryType().toString(), patientStatus, report, bundle);
            switch (value.getQueryType()) {
                case INITIAL -> {
                    updateReportability(patientStatus, report, measureReport);
                    produceResourceEvaluatedRecords(value.getQueryType(), patientStatus, report, measureReport);
                }
                case SUPPLEMENTAL -> produceResourceEvaluatedRecords(value.getQueryType(), patientStatus, report, measureReport);
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
            Attributes attributes = Attributes.builder().put(stringKey("facilityId"), patientStatus.getFacilityId()).
                    put(stringKey("patientId"), patientStatus.getPatientId()).
                    put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();
            if (reportablePatient) {
                measureEvalMetrics.IncrementPatientReportableCounter(attributes);
            } else {
                measureEvalMetrics.IncrementPatientNonReportableCounter(attributes);
            }
        }
    }

    private MeasureReport evaluateMeasure (
            String queryType,
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            Bundle bundle) {

        long start = System.currentTimeMillis();

        String measureId = report.getReportType();
        if (logger.isDebugEnabled()) {
            logger.debug("Evaluating measure: {}", measureId);
        }
        MeasureEvaluator measureEvaluator = measureEvaluatorCache.get(measureId);
        if (measureEvaluator == null) {
            throw new IllegalStateException(String.format("Unknown measure: %s", measureId));
        }
        MeasureReport measureReport = measureEvaluator.evaluate(
                report.getStartDate(),
                report.getEndDate(),
                patientStatus.getPatientId(),
                bundle);
        if (logger.isDebugEnabled()) {
            logger.debug("Population counts: {}", measureReport.getGroup().stream()
                    .flatMap(group -> group.getPopulation().stream())
                    .map(population -> String.format(
                            "%s=[%d]",
                            population.getCode().getCodingFirstRep().getCode(),
                            population.getCount()))
                    .collect(Collectors.joining(" ")));
        }

        long timeElapsed = System.currentTimeMillis() - start;
        Attributes attributes = Attributes.builder().put(stringKey("facilityId"), patientStatus.getFacilityId()).
                put(stringKey("patientId"), patientStatus.getPatientId()).
                put(stringKey("reportTypes"), report.getReportType()).
                put(stringKey("frequency"), report.getFrequency()).
                put(stringKey("startDate"), report.getStartDate().toString()).
                put(stringKey("endDate"), report.getEndDate().toString()).
                put(stringKey("queryType"), queryType).
                put(stringKey("correlationId"), patientStatus.getCorrelationId()).build();
        if (logger.isInfoEnabled()) {
            logger.info("Measure evaluation duration for Patient {} on {} query: {}", patientStatus.getPatientId(), queryType, timeElapsed + " milliseconds");
        }

        // Record the duration of the evaluation
        measureEvalMetrics.MeasureEvalDuration(timeElapsed, attributes);

        return measureReport;
    }

    private void updateReportability (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {
        report.setReportable(reportabilityPredicate.test(measureReport));
        patientStatusRepository.save(patientStatus);
    }

    private void produceResourceEvaluatedRecords (
            QueryType phase,
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {

        if (logger.isDebugEnabled()) {
            logger.debug("Producing {} records", Topics.RESOURCE_EVALUATED);
        }

        var list = measureReportNormalizer.normalize(measureReport);

        if (phase == QueryType.INITIAL && !report.getReportable()) { // produce Evaluated Resource the Initial phase only if the measure is not reportable
            list.stream().filter(resource -> resource instanceof MeasureReport).findFirst().ifPresent(measure -> produceResourceEvaluatedRecord(patientStatus, report, measure.getIdPart(), measure));
        }  else if (phase == QueryType.SUPPLEMENTAL && report.getReportable())  { //produce Evaluated Resource only on the Supplemental phase if the measure is reportable
            for (Resource resource : list) {
                produceResourceEvaluatedRecord(patientStatus, report, measureReport.getIdPart(), resource);
            }
        }


    }

    private void produceResourceEvaluatedRecord (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            String measureReportId,
            Resource resource) {

        if (logger.isTraceEnabled()) {
            logger.trace(
                    "Producing {} record: {}/{}",
                    Topics.RESOURCE_EVALUATED, resource.getResourceType(), resource.getIdPart());
        }
        ResourceEvaluated.Key key = new ResourceEvaluated.Key();
        key.setFacilityId(patientStatus.getFacilityId());
        key.setStartDate(report.getStartDate());
        key.setEndDate(report.getEndDate());
        key.setFrequency(report.getFrequency());
        ResourceEvaluated value = new ResourceEvaluated();
        value.setMeasureReportId(measureReportId);
        value.setPatientId(patientStatus.getPatientId());
        value.setResource(resource);
        value.setReportType(report.getReportType());
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

    private void produceDataAcquisitionRequestedRecord (T value, PatientReportingEvaluationStatus patientStatus) {
        if (logger.isDebugEnabled()) {
            logger.debug("Producing {} record", Topics.DATA_ACQUISITION_REQUESTED);
        }
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
