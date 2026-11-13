package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.clients.admin.AdminClient;
import org.apache.kafka.clients.admin.AdminClientConfig;
import org.apache.kafka.clients.admin.NewTopic;
import org.apache.kafka.clients.producer.KafkaProducer;
import org.apache.kafka.clients.producer.ProducerConfig;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.errors.TopicExistsException;
import org.apache.kafka.common.serialization.StringSerializer;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.List;
import java.util.Properties;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;

class KafkaPartitionProofTest {

    @ParameterizedTest
    @ValueSource(ints = {3, 6, 12})
    void dataAcquisitionRequestedKeyLandsOnTheMurmur2Partition(int partitionCount) throws Exception {
        String bootstrap = System.getenv("KAFKA_BOOTSTRAP");
        if (bootstrap == null || bootstrap.isBlank()) {
            return;
        }

        String key = KafkaKeys.forPatient("facility-proof", "patient-proof");
        String topic = "proof-data-acquisition-requested-" + partitionCount;
        Properties adminProps = new Properties();
        adminProps.put(AdminClientConfig.BOOTSTRAP_SERVERS_CONFIG, bootstrap);
        adminProps.put(AdminClientConfig.REQUEST_TIMEOUT_MS_CONFIG, "15000");
        try (AdminClient admin = AdminClient.create(adminProps)) {
            try {
                admin.createTopics(List.of(new NewTopic(topic, partitionCount, (short) 1)))
                        .all()
                        .get(15, TimeUnit.SECONDS);
            } catch (ExecutionException ex) {
                if (!(ex.getCause() instanceof TopicExistsException)) {
                    throw ex;
                }
            }
        }

        Properties producerProps = new Properties();
        producerProps.put(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG, bootstrap);
        producerProps.put(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG, StringSerializer.class.getName());
        producerProps.put(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG, StringSerializer.class.getName());
        producerProps.put(ProducerConfig.ACKS_CONFIG, "all");
        producerProps.put(ProducerConfig.ENABLE_IDEMPOTENCE_CONFIG, "true");
        producerProps.put(ProducerConfig.CLIENT_ID_CONFIG, "proof-java-p");
        try (KafkaProducer<String, String> producer = new KafkaProducer<>(producerProps)) {
            int partition = producer.send(new ProducerRecord<>(topic, key, key)).get(15, TimeUnit.SECONDS).partition();
            assertEquals(KafkaMurmur2.partition(key, partitionCount), partition);
            String results = System.getenv("KAFKA_PROOF_RESULTS");
            if (results != null && !results.isBlank()) {
                Path dir = Path.of(results);
                Files.createDirectories(dir);
                String line = "java\t" + topic + "\t" + partitionCount + "\t" + key + "\t" + partition + System.lineSeparator();
                Files.writeString(dir.resolve("partitions.tsv"), line, StandardCharsets.UTF_8,
                        StandardOpenOption.CREATE, StandardOpenOption.APPEND);
            }
        }
    }
}
