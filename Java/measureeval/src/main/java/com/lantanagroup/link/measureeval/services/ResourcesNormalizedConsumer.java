package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourcesNormalized;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.MeasureReport;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.Acknowledgment;
import org.springframework.stereotype.Service;

import java.util.function.Predicate;

@Service
public class ResourcesNormalizedConsumer extends AbstractResourceConsumer<ResourcesNormalized> {
  public ResourcesNormalizedConsumer (
          PatientReportingEvaluationStatusRepository patientStatusRepository,
          Predicate<MeasureReport> reportabilityPredicate,
          MeasureEvalMetrics measureEvalMetrics,
          KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
          EvaluateMeasureService evaluateMeasureService,
          PatientStatusBundler patientStatusBundler,
          BlobStorageService blobStorageService,
          @Qualifier("resourceNormalizedRecoverer") ConsumerRecordRecoverer recoverer,
          MeasureReportGeneratedProducer measureReportGeneratedProducer,
          RedisResourceService redisResourceService,
          AbsResourceService absResourceService,
          MongoOperations mongoOperations){
    super(
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

    @KafkaListener(topics = Topics.RESOURCES_NORMALIZED, containerFactory = "manualAckListenerContainerFactory")
    public void consume(
            ConsumerRecord<ResourceKey, ResourcesNormalized> record,
            Acknowledgment acknowledgment) {
        doConsume(record, acknowledgment);
    }
}
