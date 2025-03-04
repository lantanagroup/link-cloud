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
import org.apache.commons.lang3.StringUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.ResourceType;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

@Getter
@Setter
public abstract class AbstractResourceRecord {

    private boolean AcquisitionComplete;

    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private QueryType queryType;
    private IBaseResource resource;

    private ReportableEvent reportableEvent;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<ScheduledReport> scheduledReports = new ArrayList<>();

    @JsonIgnore
    public boolean isPatientResource() {
        return StringUtils.isNotEmpty(patientId);
    }

    @JsonIgnore
    public ResourceType getResourceType() {
        return ResourceType.fromCode(resource!= null?resource.fhirType():"");
    }

    @JsonIgnore
    public String getResourceId() {
        return resource!=null?resource.getIdElement().getIdPart():"";
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
