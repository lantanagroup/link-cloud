package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.AbstractResource;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.records.ResourceNormalized;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
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

@Service
public class ResourceNormalizedConsumer extends BaseConsumer<ResourceNormalized>{
    private static final Logger logger = LoggerFactory.getLogger(ResourceNormalizedConsumer.class);
    public ResourceNormalizedConsumer(
            MongoOperations mongoOperations,
            DataAcquisitionClient dataAcquisitionClient,
            MeasureEvaluatorCache measureEvaluatorCache,
            Predicate<MeasureReport> reportabilityPredicate,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate) {
        super(mongoOperations, dataAcquisitionClient, measureEvaluatorCache, reportabilityPredicate, dataAcquisitionRequestedTemplate, resourceEvaluatedTemplate);
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
        this.getMongoOperations().save(resource);

        logger.info("Beginning patient status update");
        PatientReportingEvaluationStatus patientStatus = Objects.requireNonNullElseGet(
                retrievePatientStatus(facilityId, correlationId),
                () -> createPatientStatus(facilityId, correlationId, value));
        if (!patientStatus.hasQueryType(value.getQueryType())) {
            updatePatientStatus(facilityId, correlationId, value, patientStatus);
        }
        updatePatientStatusResource(value, patientStatus, NormalizationStatus.NORMALIZED);
        this.getMongoOperations().save(patientStatus);
        if (!isReadyToEvaluate(patientStatus)) {
            logger.info("Not ready to evaluate; exiting");
            return;
        }

        logger.info("Beginning measure evaluation");
        Bundle bundle = createBundle(patientStatus);
        evaluateMeasures(value, patientStatus, bundle);
    }

}
