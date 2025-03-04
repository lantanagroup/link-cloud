package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class PatientResource extends AbstractResourceEntity {
    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;
}
