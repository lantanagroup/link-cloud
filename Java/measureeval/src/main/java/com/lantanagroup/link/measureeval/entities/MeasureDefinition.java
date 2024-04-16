package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Field;

import java.time.Instant;

@Getter @Setter
@JsonInclude(JsonInclude.Include.NON_NULL)
public class MeasureDefinition {
    @Id
    private String id;

    @Field("measureDefinitionId")
    private String measureId;

    @Field("measureDefinitionName")
    private String measureName;

    @Field("url")
    private String measureUrl;

    private Instant lastUpdated;

    private Bundle bundle;
}
