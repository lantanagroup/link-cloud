package com.lantanagroup.link.shared.entities;

import com.fasterxml.jackson.annotation.JsonFormat;
import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.Bundle;

import java.util.Date;

@Getter
@Setter
public class PatientSubmissionModel {
    private String facilityId;

    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private String reportScheduleId;
    @JsonFormat(pattern = "yyyy-MM-dd'T'HH:mm:ss.SSSX", shape = JsonFormat.Shape.STRING)
    private Date startDate;
    @JsonFormat(pattern = "yyyy-MM-dd'T'HH:mm:ss.SSSX", shape = JsonFormat.Shape.STRING)
    private Date endDate;
    private Bundle bundle;
}
