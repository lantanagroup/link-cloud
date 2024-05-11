package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.instance.model.api.IIdType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.Resource;
import org.springframework.stereotype.Service;

import java.util.UUID;

@Service
public class MeasureReportNormalizer {
    private final FhirContext fhirContext;

    public MeasureReportNormalizer(FhirContext fhirContext) {
        this.fhirContext = fhirContext;
    }

    public void normalize(MeasureReport measureReport) {
        if (!measureReport.hasId()) {
            measureReport.setId(UUID.randomUUID().toString());
        }
        measureReport.getContained().forEach(this::normalizeContained);
        fhirContext.newTerser().getAllPopulatedChildElementsOfType(measureReport, Reference.class)
                .forEach(this::normalizeReference);
        // TODO: Ensure these are local references
        measureReport.setEvaluatedResource(measureReport.getContained().stream()
                .map(Resource::getIdElement)
                .map(Reference::new)
                .toList());
    }

    private void normalizeContained(Resource contained) {
        normalizeId(contained.getIdElement());
    }

    private void normalizeReference(Reference reference) {
        IIdType id = reference.getReferenceElement();
        reference.setReferenceElement(normalizeId(id));
    }

    private IIdType normalizeId(IIdType id) {
        return id.setParts(
                id.getBaseUrl(),
                id.getResourceType(),
                normalizeIdPart(id.getIdPart()),
                id.getVersionIdPart());
    }

    private String normalizeIdPart(String idPart) {
        if (idPart == null) {
            return null;
        }
        return idPart.replaceAll("(?i)^(#?)LCR-", "$1");
    }
}
