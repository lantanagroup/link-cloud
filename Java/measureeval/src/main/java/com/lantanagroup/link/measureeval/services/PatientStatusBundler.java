package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.*;
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

        var sharedResourcesRefs = patientStatus.getResources().stream()
                .filter(resource -> resource.getNormalizationStatus() == NormalizationStatus.NORMALIZED)
                .filter(resource -> !resource.getIsPatientResource())
                .toList();

        logger.debug("Collecting patient resources for patient {} and {} shared resources from the database", patientStatus.getPatientId(), sharedResourcesRefs.size());

        var patientResources = resourceRepository.findPatientResources(patientStatus.getFacilityId(), patientStatus.getPatientId());
        var sharedResources = resourceRepository.findSharedResources(patientStatus.getFacilityId(), sharedResourcesRefs);

        logger.debug("Retrieved {} patient resources and {} shared resources from the database", patientResources.size(), sharedResources.size());

        List<AbstractResourceEntity> resources = new ArrayList<>();
        resources.addAll(patientResources);
        resources.addAll(sharedResources);

        logger.debug("Collected a total of {} resources from the database", resources.size());
        
        return resources;
    }
}
