package com.lantanagroup.link.validation.serdes;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.model.PatientEvaluatedModel;
import org.apache.kafka.common.errors.SerializationException;
import org.apache.kafka.common.serialization.Deserializer;

import java.nio.charset.StandardCharsets;
import java.util.Map;

public class PatientEvaluatedDeserializer implements Deserializer<PatientEvaluatedModel> {
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Override
    public void configure(Map<String, ?> configs, boolean isKey) {
    }

    @Override
    public PatientEvaluatedModel deserialize(String topic, byte[] data) {
        try {
            if (data == null) {
                return null;
            }
            return objectMapper.readValue(new String(data, StandardCharsets.UTF_8), PatientEvaluatedModel.class);
        } catch (Exception e) {
            throw new SerializationException("Error when deserializing byte[] to MessageDto");
        }
    }

    @Override
    public void close() {
    }
}
