package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.*;
import com.lantanagroup.link.measureeval.repositories.AbstractResourceRepository;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.List;
import java.time.Duration;
import java.time.Instant;

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
        var patientResourcesRefs = patientStatus.getResources().stream()
                .filter(resource -> resource.getNormalizationStatus() == NormalizationStatus.NORMALIZED)
                .filter(PatientReportingEvaluationStatus.Resource::getIsPatientResource)
                .toList();

        logger.debug("Collecting patient resources for patient {} and {} shared resources from the database", patientStatus.getPatientId(), sharedResourcesRefs.size());

        Instant start = Instant.now();
        var patientResources = resourceRepository.findResources(patientStatus.getFacilityId(), false, patientResourcesRefs);
        var sharedResources = resourceRepository.findResources(patientStatus.getFacilityId(), true, sharedResourcesRefs);
        Duration elapsed = Duration.between(start, Instant.now());

        logger.debug("Retrieved {} patient resources and {} shared resources from the database in {} seconds",
                patientResources.size(), sharedResources.size(), elapsed.toMillis() / 1000.0);

        List<AbstractResourceEntity> resources = new ArrayList<>();
        resources.addAll(patientResources);
        resources.addAll(sharedResources);

        logger.debug("Collected a total of {} resources from the database", resources.size());

        return resources;
    }
}
