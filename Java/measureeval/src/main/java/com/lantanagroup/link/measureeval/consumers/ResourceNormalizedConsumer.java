package com.lantanagroup.link.measureeval.consumers;

import com.lantanagroup.link.measureeval.entities.AbstractResource;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.records.ResourceNormalized;
import org.apache.commons.lang3.StringUtils;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.stereotype.Component;

@Component
public class ResourceNormalizedConsumer {
    private final MongoOperations mongoOperations;

    public ResourceNormalizedConsumer(MongoOperations mongoOperations) {
        this.mongoOperations = mongoOperations;
    }

    @KafkaListener(topics = Topics.RESOURCE_NORMALIZED)
    public void consume(ConsumerRecord<String, ResourceNormalized> message) {
        ResourceNormalized value = message.value();
        IBaseResource resource = value.getResource();
        System.err.printf("Consuming message: %s%n", resource.fhirType());
        AbstractResource entity = StringUtils.isEmpty(value.getPatientId())
                ? new SharedResource()
                : new PatientResource();
        entity.setResource(resource);
        mongoOperations.save(entity);
    }
}
