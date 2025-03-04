package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.shared.serdes.Views;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.Id;
import org.springframework.data.annotation.LastModifiedDate;
import org.springframework.data.annotation.Version;

import java.util.Date;

@Getter
@Setter
public class MeasureDefinition {
    @Id
    @JsonView(Views.Summary.class)
    private String id;

    @JsonView(Views.Detail.class)
    private Bundle bundle;

    @Version
    @JsonView(Views.Summary.class)
    private long version;

    @CreatedDate
    @JsonView(Views.Summary.class)
    private Date createdDate;

    @LastModifiedDate
    @JsonView(Views.Summary.class)
    private Date modifiedDate;
}
