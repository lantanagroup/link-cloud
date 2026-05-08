package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.measureeval.entities.QueryType;
import com.lantanagroup.link.measureeval.entities.ReportableEvent;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.ResourceType;

import java.util.*;
import java.util.function.Function;
import java.util.stream.Collectors;

@Getter
@Setter
public abstract class AbstractResourceRecord {
    private static final Map<String, ResourceType> RESOURCE_TYPES_BY_CODE = Arrays.stream(ResourceType.values())
            .collect(Collectors.toMap(Enum::name, Function.identity()));

    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private QueryType queryType;

    private IBaseResource resource;

    private ReportableEvent reportableEvent;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<ScheduledReport> scheduledReports = new ArrayList<>();

    private boolean acquisitionComplete;

    @JsonIgnore
    public ResourceType getResourceType() {
        if (resource == null) {
            return null;
        }
        return RESOURCE_TYPES_BY_CODE.get(resource.fhirType());
    }

    @JsonIgnore
    public String getResourceId() {
        if (resource == null) {
            return null;
        }
        return resource.getIdElement().getIdPart();
    }

    @JsonIgnore
    public String getResourceTypeAndId() {
        if (resource == null) {
            return null;
        }
        return resource.getIdElement().toUnqualifiedVersionless().getValue();
    }

    @Getter
    @Setter
    public static class ScheduledReport {
        private String[] reportTypes;
        private Date startDate;
        private Date endDate;
        private String frequency;
        private String reportTrackingId;
    }
}
