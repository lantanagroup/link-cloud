package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.AbstractResourceEntity;
import com.lantanagroup.link.measureeval.entities.NormalizationStatus;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.repositories.AbstractResourceRepository;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.*;

@Service
public class PatientStatusBundler {

    private static final Logger logger = LoggerFactory.getLogger(PatientStatusBundler.class);

    private final AbstractResourceRepository resourceRepository;

    public PatientStatusBundler(AbstractResourceRepository resourceRepository) {
        this.resourceRepository = resourceRepository;
    }

    public Bundle createBundle (PatientReportingEvaluationStatus patientStatus) {
        if (logger.isDebugEnabled()) {
            logger.debug("Creating bundle");
        }
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        retrieveResources(patientStatus).stream()
                .map(AbstractResourceEntity::getResource)
                .map(Resource.class::cast)
                .forEachOrdered(resource -> bundle.addEntry().setResource(resource));
        return bundle;
    }

    private List<AbstractResourceEntity> retrieveResources (PatientReportingEvaluationStatus patientStatus) {
        if (logger.isDebugEnabled()) {
            logger.debug("Retrieving resources");
        }

        Set<Map.Entry<String, String>> singles = new HashSet<>();

        return patientStatus.getResources().stream()
                .filter(resource -> resource.getNormalizationStatus() == NormalizationStatus.NORMALIZED)
                .filter(resource -> singles.add(new AbstractMap.SimpleEntry<>(resource.getResourceType().toString(), resource.getResourceId())))
                .map(resource -> retrieveResource(patientStatus.getFacilityId(), resource))
                .toList();
    }

    private AbstractResourceEntity retrieveResource (
            String facilityId,
            PatientReportingEvaluationStatus.Resource resource) {
        logger.trace("Retrieving resource: {}/{}", resource.getResourceType(), resource.getResourceId());
        return resourceRepository.findOne(facilityId, resource);
    }
}
