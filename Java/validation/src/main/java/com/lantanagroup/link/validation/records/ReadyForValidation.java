package com.lantanagroup.link.validation.records;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.List;

@Getter
@Setter
public class ReadyForValidation {
    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private String reportTrackingId;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<String> reportTypes = new ArrayList<>();

    private String payloadUri;

    @Getter
    @Setter
    public static class Key {
        private String facilityId;
    }
}
