package com.lantanagroup.link.validation.services.execution.executors;

import org.hl7.fhir.instance.model.api.IBaseResource;

/** The location a check's finding names: the resource it is about, as {@code Type/id}, or just {@code Type} without an id. */
final class FindingLocation {

    private FindingLocation() {
    }

    static String of(IBaseResource resource) {
        String id = resource.getIdElement() != null ? resource.getIdElement().getIdPart() : null;
        return (id != null && !id.isBlank()) ? resource.fhirType() + "/" + id : resource.fhirType();
    }
}
