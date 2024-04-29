package com.lantanagroup.link.measureeval.models;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.ResourceType;

import java.util.ArrayList;
import java.util.List;

@Getter
@Setter
public class QueryResults {
    private String patientId;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<QueryResult> queryResults = new ArrayList<>();

    @Getter
    @Setter
    public static class QueryResult {
        private String resourceId;
        private ResourceType resourceType;
        private QueryType queryType;
    }
}
