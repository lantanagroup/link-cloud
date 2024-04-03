package com.lantanagroup.link.validation.consumers;

import com.lantanagroup.link.validation.model.PatientEvaluatedModel;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.stereotype.Component;

@Component
public class PatientEvaluatedConsumer {
    private static final Logger log = LoggerFactory.getLogger(PatientEvaluatedConsumer.class);

    @KafkaListener(topics = "PatientEvaluated", properties = {"spring.json.value.default.type=com.lantanagroup.link.validation.model.PatientEvaluatedModel"})
    public void listen(ConsumerRecord<String, PatientEvaluatedModel> record) {
        PatientEvaluatedModel value = record.value();
        log.info("Received PatientEvaluated:\n\tTenant ID: {}\n\tPatient ID: {}", value.getTenantId(), value.getPatientId());
    }
}