package com.lantanagroup.link.shared.audit;

import com.fasterxml.jackson.annotation.JsonProperty;
import com.fasterxml.jackson.annotation.JsonPropertyOrder;

import java.time.Instant;
import java.util.List;

/**
 * The value of an {@code AuditableEventOccurred} message, matching the .NET {@code AuditEventMessage}
 * on the wire. The PascalCase names are declared here so no naming strategy is involved; serialise it
 * only with {@link AuditJson#wireMapper()}.
 */
@JsonPropertyOrder({"FacilityId", "ServiceName", "CorrelationId", "EventDate", "UserId", "User", "Action",
        "Resource", "ResourceId", "PatientId", "PropertyChanges", "Notes"})
public record AuditEventMessage(
        @JsonProperty("FacilityId") String facilityId,
        @JsonProperty("ServiceName") String serviceName,
        @JsonProperty("CorrelationId") String correlationId,
        @JsonProperty("EventDate") Instant eventDate,
        @JsonProperty("UserId") String userId,
        @JsonProperty("User") String user,
        @JsonProperty("Action") AuditAction action,
        @JsonProperty("Resource") String resource,
        @JsonProperty("ResourceId") String resourceId,
        @JsonProperty("PatientId") String patientId,
        @JsonProperty("PropertyChanges") List<PropertyChange> propertyChanges,
        @JsonProperty("Notes") String notes) {
}
