package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourcesNormalized;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import org.hl7.fhir.r4.model.MeasureReport;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
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
          ConsumerRecordRecoverer recoverer,
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
}
