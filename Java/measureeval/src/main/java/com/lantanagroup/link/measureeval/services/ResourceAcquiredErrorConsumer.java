package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourceAcquired;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.measureeval.repositories.ResourceRepository;
import org.hl7.fhir.r4.model.MeasureReport;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

import java.util.function.Predicate;

@Service
public class ResourceAcquiredErrorConsumer extends AbstractResourceConsumer<ResourceAcquired> {
    public ResourceAcquiredErrorConsumer(
            ResourceRepository resourceRepository,
            PatientReportingEvaluationStatusRepository patientStatusRepository,
            Predicate<MeasureReport> reportabilityPredicate,
            MeasureEvalMetrics measureEvalMetrics,
            KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate,
            EvaluateMeasureService evaluateMeasureService,
            PatientStatusBundler patientStatusBundler,
            ResourceEvaluatedProducer resourceEvaluatedProducer){
        super(
                resourceRepository,
                patientStatusRepository,
                reportabilityPredicate,
                measureEvalMetrics,
                dataAcquisitionRequestedTemplate,
                evaluateMeasureService,
                patientStatusBundler,
                resourceEvaluatedProducer,
                null);
    }
}
