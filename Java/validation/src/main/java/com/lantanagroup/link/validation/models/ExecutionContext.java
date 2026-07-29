package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.JsonNode;
import lombok.*;
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
    private IBaseResource resource;
    @Builder.Default
    private List<IBaseResource> bundleEntries = Collections.emptyList();
    private JsonNode rawPayload;
    private Map<String, Object> contextVars;
    private String requestor;
    private OffsetDateTime requestedAt;
}
