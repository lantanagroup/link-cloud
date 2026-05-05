package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
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
    private final IParser fhirJsonParser;

    public PatientStatusBundler(ResourceRepository resourceRepository, FhirContext fhirContext) {
        this.resourceRepository = resourceRepository;
        this.fhirJsonParser = fhirContext.newJsonParser();
    }


    public Bundle createBundle (String facilityId, String correlationId) {
        if (logger.isDebugEnabled()) {
            logger.debug("Creating bundle from Mongo");
        }
        return buildBundle(retrieveResources(facilityId, correlationId));
    }

    public Bundle createBundleFromResources(List<Resource> resources) {
        if (logger.isDebugEnabled()) {
            logger.debug("Creating bundle from {} pre-loaded resources", resources.size());
        }
        return buildBundle(resources);
    }

    private Bundle buildBundle(List<Resource> resources) {
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        for (Resource r : resources) {
            org.hl7.fhir.r4.model.Resource parsed =
                    (org.hl7.fhir.r4.model.Resource) fhirJsonParser.parseResource(r.getResource());
            bundle.addEntry().setResource(parsed);
        }
        bundle.setTotal(bundle.getEntry().size());
        return bundle;
    }

    private List<Resource> retrieveResources(String facilityId, String correlationId) {
        if (logger.isDebugEnabled()) {
            logger.debug("Retrieving resources from Mongo");
        }

        try (Timer timer = Timer.start()) {
            var resources  = resourceRepository.findByFacilityIdAndCorrelationId(facilityId, correlationId);

            logger.debug("Retrieved {} resources from the database in {} seconds",
                    resources.size(), timer.getSeconds());

            return resources;
        }
    }
}
