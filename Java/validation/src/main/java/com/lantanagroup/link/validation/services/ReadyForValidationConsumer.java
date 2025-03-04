package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import com.lantanagroup.link.validation.records.ValidationComplete;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.messaging.handler.annotation.Header;
import org.springframework.stereotype.Service;

import java.util.List;

@Service
public class ReadyForValidationConsumer {
    private final ReportClient reportClient;
    private final ValidationService validationService;
    private final CategorizationService categorizationService;
    private final ResultRepository resultRepository;
    private final KafkaTemplate<ValidationComplete.Key, ValidationComplete> validationCompleteTemplate;

    public ReadyForValidationConsumer(
            ReportClient reportClient,
            ValidationService validationService,
            CategorizationService categorizationService,
            ResultRepository resultRepository,
            KafkaTemplate<ValidationComplete.Key, ValidationComplete> validationCompleteTemplate) {
        this.reportClient = reportClient;
        this.validationService = validationService;
        this.categorizationService = categorizationService;
        this.resultRepository = resultRepository;
        this.validationCompleteTemplate = validationCompleteTemplate;
    }

    @KafkaListener(topics = Topics.READY_FOR_VALIDATION)
    public void consume(
            @Header(Headers.CORRELATION_ID) String correlationId,
            ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> record) {
        String facilityId = record.key().getFacilityId();
        String reportId = record.key().getReportId();
        String patientId = record.value().getPatientId();
        Bundle bundle = reportClient.getSubmissionBundle(facilityId, reportId, patientId);
        List<Result> results = validationService.validate(bundle);
        for (Result result : results) {
            result.setFacilityId(facilityId);
            result.setReportId(reportId);
            result.setPatientId(patientId);
        }
        categorizationService.categorize(results);
        resultRepository.saveAll(results);
        boolean valid = results.stream()
                .flatMap(result -> result.getCategories().stream())
                .allMatch(Category::isAcceptable);
        ValidationComplete.Key key = new ValidationComplete.Key();
        key.setFacilityId(facilityId);
        key.setReportId(reportId);
        ValidationComplete value = new ValidationComplete();
        value.setPatientId(patientId);
        value.setValid(valid);
        org.apache.kafka.common.header.Headers headers = new RecordHeaders()
                .add(Headers.CORRELATION_ID, Headers.getBytes(correlationId));
        validationCompleteTemplate.send(new ProducerRecord<>(Topics.VALIDATION_COMPLETE, null, key, value, headers));
    }
}
