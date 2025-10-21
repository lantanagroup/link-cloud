package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.repositories.ResourceRepository;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.ResourceType;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class PatientStatusBundlerTest {

    private PatientStatusBundler bundler;

    @BeforeEach
    void setUp() {
        ResourceRepository resourceRepository = mock(ResourceRepository.class);
        FhirContext fhirContext = FhirContext.forR4();
        bundler = new PatientStatusBundler(resourceRepository, fhirContext);
    }

    @Test
    void createBundleFromResources_multipleResources_buildsBundle() {
        List<Resource> resources = new ArrayList<>();
        resources.add(makeResource(ResourceType.Patient, "p1",
                "{\"resourceType\":\"Patient\",\"id\":\"p1\"}"));
        resources.add(makeResource(ResourceType.Encounter, "e1",
                "{\"resourceType\":\"Encounter\",\"id\":\"e1\"}"));
        resources.add(makeResource(ResourceType.Condition, "c1",
                "{\"resourceType\":\"Condition\",\"id\":\"c1\"}"));

        Bundle bundle = bundler.createBundleFromResources(resources);

        assertEquals(Bundle.BundleType.COLLECTION, bundle.getType());
        assertEquals(3, bundle.getTotal());
        assertEquals(3, bundle.getEntry().size());
    }

    @Test
    void createBundleFromResources_emptyList_returnsEmptyBundle() {
        Bundle bundle = bundler.createBundleFromResources(List.of());

        assertEquals(Bundle.BundleType.COLLECTION, bundle.getType());
        assertEquals(0, bundle.getTotal());
        assertTrue(bundle.getEntry().isEmpty());
    }

    @Test
    void createBundleFromResources_preservesResourceTypes() {
        List<Resource> resources = new ArrayList<>();
        resources.add(makeResource(ResourceType.Patient, "p1",
                "{\"resourceType\":\"Patient\",\"id\":\"p1\"}"));
        resources.add(makeResource(ResourceType.Observation, "o1",
                "{\"resourceType\":\"Observation\",\"id\":\"o1\",\"status\":\"final\"}"));

        Bundle bundle = bundler.createBundleFromResources(resources);

        assertEquals(2, bundle.getEntry().size());
        assertInstanceOf(Patient.class, bundle.getEntry().get(0).getResource());
        assertInstanceOf(Observation.class, bundle.getEntry().get(1).getResource());
    }

    @Test
    void createBundleFromResources_singleResource_bundleHasTotalOne() {
        List<Resource> resources = List.of(
                makeResource(ResourceType.Patient, "p1",
                        "{\"resourceType\":\"Patient\",\"id\":\"p1\"}")
        );

        Bundle bundle = bundler.createBundleFromResources(resources);

        assertEquals(1, bundle.getTotal());
        assertEquals(1, bundle.getEntry().size());
    }

    @Test
    void createBundleFromResources_preservesResourceJsonAfterParsing() {
        List<Resource> resources = new ArrayList<>();
        resources.add(makeResource(ResourceType.Patient, "p1",
                "{\"resourceType\":\"Patient\",\"id\":\"p1\"}"));
        resources.add(makeResource(ResourceType.Encounter, "e1",
                "{\"resourceType\":\"Encounter\",\"id\":\"e1\"}"));

        bundler.createBundleFromResources(resources);

        for (Resource r : resources) {
            assertNotNull(r.getResource());
        }
    }

    private Resource makeResource(ResourceType type, String id, String json) {
        Resource r = new Resource();
        r.setFacilityId("facility-1");
        r.setCorrelationId("corr-1");
        r.setPatientId("patient-1");
        r.setResourceType(type);
        r.setResourceId(id);
        r.setResource(json);
        return r;
    }
}
