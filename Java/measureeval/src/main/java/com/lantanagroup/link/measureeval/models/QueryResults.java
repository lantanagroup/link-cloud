package com.lantanagroup.link.measureeval.models;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.measureeval.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.ResourceType;

import java.util.ArrayList;
import java.util.List;

@Getter
@Setter
public class QueryResults {
    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<QueryResult> queryResults = new ArrayList<>();

    @Getter
    @Setter
    public static class QueryResult {
        private ResourceType resourceType;

        @JsonDeserialize(using = FhirIdDeserializer.class)
        private String resourceId;

        private QueryType queryType;

        public IdType getIdElement() {
            return new IdType(resourceType.name(), resourceId);
        }
    }
}
