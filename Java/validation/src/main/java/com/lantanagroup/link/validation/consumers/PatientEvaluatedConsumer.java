package com.lantanagroup.link.validation.consumers;

import com.lantanagroup.link.validation.model.PatientEvaluatedModel;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.stereotype.Component;

@Component
public class PatientEvaluatedConsumer {
    @KafkaListener(topics = "PatientEvaluated", containerFactory = "kafkaListenerContainerFactory")
    public void listen(ConsumerRecord<String, PatientEvaluatedModel> record) {
        System.out.println("Received message key: " + record.key());
        // Process your message here
    }
}