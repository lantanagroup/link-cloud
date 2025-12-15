package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.util.Date;

@Getter
@Setter
@AllArgsConstructor
@NoArgsConstructor
public class MeasureReportGenerated {
    private String facilityId;
    private String reportTrackingId;

    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private String reportType;
    private String measureReportId;
    private String measureReportURI;
    private String measureReportFileName;
    private boolean isReportable;
}
