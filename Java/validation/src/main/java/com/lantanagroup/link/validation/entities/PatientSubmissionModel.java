package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;

import java.util.Date;

@Getter
@Setter
public class PatientSubmissionModel {
    private String facilityId;

    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private String reportScheduleId;
    private Date startDate;
    private Date endDate;
    private String patientResources;
    private String otherResources;
}
