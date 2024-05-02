package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.lantanagroup.link.measureeval.models.QueryType;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.ResourceType;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

public class BaseResource {

  @Getter
  private IBaseResource resource;
  @Getter
  private String patientId;

  @Getter
  private QueryType queryType;

  @JsonSetter(nulls = Nulls.AS_EMPTY)
  @Getter
  private List<ScheduledReport> scheduledReports = new ArrayList<>();

  @Getter
  @Setter
  public static class ScheduledReport {
    private String reportType;
    private Date startDate;
    private Date endDate;
  }

  public ResourceType getResourceType() {
    return ResourceType.fromCode(resource.fhirType());
  }

  public String getResourceId() {
    return resource.getIdElement().getIdPart();
  }

  public boolean isPatientResource() {
    return StringUtils.isNotEmpty(patientId);
  }


}
