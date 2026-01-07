package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.repositories.ResourceRepository;
import com.lantanagroup.link.shared.Timer;
import org.hl7.fhir.r4.model.Bundle;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.List;

@Service
public class PatientStatusBundler {

    private static final Logger logger = LoggerFactory.getLogger(PatientStatusBundler.class);

    private final ResourceRepository resourceRepository;

    public PatientStatusBundler(ResourceRepository resourceRepository) {
        this.resourceRepository = resourceRepository;
    }

    public Bundle createBundle (String facilityId, String correlationId) {
        if (logger.isDebugEnabled()) {
            logger.debug("Creating bundle");
        }
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        retrieveResources(facilityId, correlationId).stream()
                .map(Resource::getResource)
                .map(org.hl7.fhir.r4.model.Resource.class::cast)
                .forEachOrdered(resource -> bundle.addEntry().setResource(resource));
        bundle.setTotal(bundle.getEntry().size());
        return bundle;
    }

    private List<Resource> retrieveResources(String facilityId, String correlationId) {
        if (logger.isDebugEnabled()) {
            logger.debug("Retrieving resources");
        }

        try (Timer timer = Timer.start()) {
            var resources  = resourceRepository.findByFacilityIdAndCorrelationId(facilityId, correlationId);

            logger.debug("Retrieved {} resources from the database in {} seconds",
                    resources.size(), timer.getSeconds());

            return resources;
        }
    }
}
