package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ApplicableContextValidatorTest {

    private static final ObjectMapper JSON = new ObjectMapper();
    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    private final ApplicableContextValidator validator = new ApplicableContextValidator(FHIR_CONTEXT);

    private List<String> validate(String json) {
        try {
            JsonNode node = json == null ? null : JSON.readTree(json);
            List<String> errors = new ArrayList<>();
            validator.validate(node, errors);
            return errors;
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    @Test
    @DisplayName("absent applicableContext is allowed")
    void nullContext() {
        assertThat(validate(null)).isEmpty();
        assertThat(validate("null")).isEmpty();
    }

    @Test
    @DisplayName("full valid context passes")
    void validContext() {
        assertThat(validate("{\"fhirResources\":[\"Bundle\",\"Patient\",\"Observation\"],"
                + "\"workflowTags\":[\"submission\",\"pre-qualification\"]}")).isEmpty();
    }

    @Test
    @DisplayName("non-object rejected")
    void nonObject() {
        assertThat(validate("[]")).anyMatch(e -> e.contains("must be a JSON object"));
    }

    @Test
    @DisplayName("unknown key rejected by name")
    void unknownKey() {
        assertThat(validate("{\"facilityIds\":[\"f1\"]}"))
                .anyMatch(e -> e.contains("unknown property 'facilityIds'"));
    }

    @Test
    @DisplayName("invalid FHIR resource type name rejected")
    void invalidResourceType() {
        assertThat(validate("{\"fhirResources\":[\"Patiant\"]}"))
                .anyMatch(e -> e.contains("'Patiant' is not a valid FHIR R4 resource type"));
    }

    @Test
    @DisplayName("fhirResources must be a non-empty array of unique non-blank strings")
    void badResourceArray() {
        assertThat(validate("{\"fhirResources\":\"Bundle\"}"))
                .anyMatch(e -> e.contains("must be an array of strings"));
        assertThat(validate("{\"fhirResources\":[]}"))
                .anyMatch(e -> e.contains("must not be empty when present"));
        assertThat(validate("{\"fhirResources\":[\"Bundle\",\"Bundle\"]}"))
                .anyMatch(e -> e.contains("duplicate value 'Bundle'"));
        assertThat(validate("{\"fhirResources\":[\"  \"]}"))
                .anyMatch(e -> e.contains("must be a non-blank string"));
        assertThat(validate("{\"fhirResources\":[42]}"))
                .anyMatch(e -> e.contains("must be a non-blank string"));

        StringBuilder many = new StringBuilder("{\"fhirResources\":[");
        for (int i = 0; i < 51; i++) {
            if (i > 0) many.append(',');
            many.append("\"R").append(i).append('"');
        }
        many.append("]}");
        assertThat(validate(many.toString()))
                .anyMatch(e -> e.contains("at most 50 entries are allowed"));
    }

    @Test
    @DisplayName("workflowTags: blank, duplicate, over-length, too many entries rejected")
    void badWorkflowTags() {
        assertThat(validate("{\"workflowTags\":[\"\"]}"))
                .anyMatch(e -> e.contains("must be a non-blank string"));
        assertThat(validate("{\"workflowTags\":[\"submission\",\"submission\"]}"))
                .anyMatch(e -> e.contains("duplicate value 'submission'"));
        assertThat(validate("{\"workflowTags\":[\"" + "t".repeat(129) + "\"]}"))
                .anyMatch(e -> e.contains("must be at most 128 characters"));

        StringBuilder many = new StringBuilder("{\"workflowTags\":[");
        for (int i = 0; i < 51; i++) {
            if (i > 0) many.append(',');
            many.append("\"tag").append(i).append('"');
        }
        many.append("]}");
        assertThat(validate(many.toString()))
                .anyMatch(e -> e.contains("at most 50 entries are allowed"));
    }
}
