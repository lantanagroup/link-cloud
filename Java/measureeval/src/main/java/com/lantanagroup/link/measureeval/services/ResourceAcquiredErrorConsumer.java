package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.kafka.Headers;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourceAcquired;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.repositories.AbstractResourceRepository;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.MeasureReport;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.messaging.handler.annotation.Header;
import org.springframework.stereotype.Service;

import java.util.function.Predicate;

@Service
public class ResourceAcquiredErrorConsumer extends AbstractResourceConsumer<ResourceAcquired> {
    public ResourceAcquiredErrorConsumer(
            AbstractResourceRepository resourceRepository,
            PatientReportingEvaluationStatusRepository patientStatusRepository,
            MeasureEvaluatorCache measureEvaluatorCache,
            MeasureReportNormalizer measureReportNormalizer,
            Predicate<MeasureReport> reportabilityPredicate,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            @Qualifier("compressedKafkaTemplate")
            KafkaTemplate<ResourceEvaluated.Key, ResourceEvaluated> resourceEvaluatedTemplate,
            MeasureEvalMetrics measureEvalMetrics){
        super(
                resourceRepository,
                patientStatusRepository,
                measureEvaluatorCache,
                measureReportNormalizer,
                reportabilityPredicate,
                dataAcquisitionRequestedTemplate,
                resourceEvaluatedTemplate,
                measureEvalMetrics);
    }

    @Override
    protected NormalizationStatus getNormalizationStatus() {
        return NormalizationStatus.ERROR;
    }

    @KafkaListener(topics = Topics.RESOURCE_ACQUIRED_ERROR)
    public void consume(
            @Header(Headers.CORRELATION_ID) String correlationId,
            ConsumerRecord<String, ResourceAcquired> record) {
        doConsume(correlationId, record);
    }
}
