package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.Resource;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.function.Function;
import java.util.stream.Collectors;

@Service
public class MeasureReportNormalizer {
    private final FhirContext fhirContext;

    public MeasureReportNormalizer(FhirContext fhirContext) {
        this.fhirContext = fhirContext;
    }

    public List<Resource> normalize(MeasureReport measureReport) {
        if (!measureReport.hasId()) {
            measureReport.setId(UUID.randomUUID().toString());
        }
        List<Resource> contained = measureReport.getContained();
        Map<String, Resource> containedByIdPart = contained.stream().collect(Collectors.toMap(
                resource -> stripHash(resource.getIdPart()),
                Function.identity()));
        measureReport.setContained(null);
        measureReport.setEvaluatedResource(null);
        for (Resource resource : contained) {
            String idPart = resource.getIdPart().replaceAll("(?i)^#?LCR-", "");
            IdType id = new IdType(resource.getResourceType().name(), idPart);
            resource.setIdElement(id);
            measureReport.addEvaluatedResource(new Reference(id));
        }
        for (Reference reference :
                fhirContext.newTerser().getAllPopulatedChildElementsOfType(measureReport, Reference.class)) {
            String idPart = stripHash(reference.getReferenceElement().getIdPart());
            Resource resource = containedByIdPart.get(idPart);
            if (resource == null) {
                continue;
            }
            reference.setReferenceElement(resource.getIdElement());
        }
        List<Resource> normalized = new ArrayList<>(contained);
        normalized.add(measureReport);
        return normalized;
    }

    private String stripHash(String idPart) {
        if (idPart == null) {
            return null;
        }
        return idPart.replaceAll("^#", "");
    }
}
