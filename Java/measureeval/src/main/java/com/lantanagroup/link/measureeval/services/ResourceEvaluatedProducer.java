package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.models.QueryType;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

@Service
public class ResourceEvaluatedProducer {
    private static final Logger logger = LoggerFactory.getLogger(ResourceEvaluatedProducer.class);
    private final MeasureReportNormalizer measureReportNormalizer;
    @Qualifier("compressedKafkaTemplate")
    private final KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate;

    public ResourceEvaluatedProducer(MeasureReportNormalizer measureReportNormalizer, KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate) {
        this.measureReportNormalizer = measureReportNormalizer;
        this.resourceEvaluatedTemplate = resourceEvaluatedTemplate;
    }

    public void produceResourceEvaluatedRecords (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            MeasureReport measureReport) {

        if (logger.isDebugEnabled()) {
            logger.debug("Producing {} records", Topics.RESOURCE_EVALUATED);
        }

        var list = measureReportNormalizer.normalize(measureReport);
        for (Resource resource : list) {
            produceResourceEvaluatedRecord(patientStatus, report, measureReport.getIdPart(), resource);
        }
    }

    public void produceResourceEvaluatedRecords (
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

    public void produceResourceEvaluatedRecord (
            PatientReportingEvaluationStatus patientStatus,
            PatientReportingEvaluationStatus.Report report,
            String measureReportId,
            Resource resource) {

        if (patientStatus == null || report == null || measureReportId == null || resource == null) {
            throw new IllegalArgumentException("All parameters are required");
        }

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
        value.setReportTrackingId(report.getReportTrackingId());

        org.apache.kafka.common.header.Headers headers = new RecordHeaders()
                .add(Headers.CORRELATION_ID, Headers.getBytes(patientStatus.getCorrelationId()));

        resourceEvaluatedTemplate.send(new ProducerRecord<>(
                Topics.RESOURCE_EVALUATED,
                null,
                key,
                value,
                headers));
    }
}
