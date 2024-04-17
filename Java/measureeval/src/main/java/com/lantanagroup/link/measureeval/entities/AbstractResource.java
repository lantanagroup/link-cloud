package com.lantanagroup.link.measureeval.entities;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.ResourceType;

import java.time.Instant;

@Getter
@Setter
public abstract class AbstractResource {
    private String id;
    private String facilityId;
    private String resourceId;
    private ResourceType resourceType;
    private IBaseResource resource;
    private Instant createdDate;
    private Instant modifiedDate;
}
