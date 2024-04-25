package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.data.annotation.Id;

import java.time.Instant;

@Getter @Setter
@JsonInclude(JsonInclude.Include.NON_NULL)
public class MeasureDefinition {
    @Id
    private String id;

    private Instant lastUpdated;

    private Bundle bundle;
}
