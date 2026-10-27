package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.JsonNode;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.time.OffsetDateTime;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.UUID;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class ExecutionContext {
    private UUID requestId;
    private String correlationId;
    private SubjectDto subject;
    // optional; null when the payload only carries bundle entries
    private IBaseResource resource;
    @Builder.Default
    private List<IBaseResource> bundleEntries = Collections.emptyList();

    // @Builder.Default doesn't cover the no-args constructor or setters, so guard against null here
    public List<IBaseResource> getBundleEntries() {
        return bundleEntries != null ? bundleEntries : Collections.emptyList();
    }

    @Builder.Default
    private Map<String, IBaseResource> referenceIndex = Collections.emptyMap();

    public Map<String, IBaseResource> getReferenceIndex() {
        return referenceIndex != null ? referenceIndex : Collections.emptyMap();
    }

    private JsonNode rawPayload;
    private Map<String, Object> contextVars;
    private String requestor;
    private OffsetDateTime requestedAt;
}
