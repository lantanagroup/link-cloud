package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.io.IOException;
import java.util.List;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class ArtifactServiceTest {
    private ArtifactService artifactService;

    @Mock
    private ArtifactRepository artifactRepository;

    private final FhirContext fhirContext = FhirContext.forR4();

    @BeforeEach
    void setUp() {
        artifactService = new ArtifactService(fhirContext, artifactRepository);
    }

    @Test
    void getTerminologyDependencies() throws IOException {
        StructureDefinition sd = new StructureDefinition();
        sd.setUrl("http://example.org/StructureDefinition/test");

        // Snapshot with binding
        ElementDefinition ed = sd.getSnapshot().addElement();
        ed.setPath("Patient.gender");
        ed.getBinding().setValueSet("http://hl7.org/fhir/ValueSet/administrative-gender|4.0.1");

        // Snapshot with fixed Coding (with version)
        ElementDefinition ed2 = sd.getSnapshot().addElement();
        ed2.setPath("Patient.extension");
        Coding fixedCoding = new Coding("http://example.org/CodeSystem/test", "code1", "Code 1");
        fixedCoding.setVersion("1.2.3");
        ed2.setFixed(fixedCoding);

        // Differential with binding (no version)
        ElementDefinition ed3 = sd.getDifferential().addElement();
        ed3.setPath("Patient.maritalStatus");
        ed3.getBinding().setValueSet("http://hl7.org/fhir/ValueSet/marital-status");

        // Differential with pattern CodeableConcept
        ElementDefinition ed4 = sd.getDifferential().addElement();
        ed4.setPath("Patient.communication.language");
        CodeableConcept patternCC = new CodeableConcept();
        patternCC.addCoding(new Coding("http://example.org/CodeSystem/lang", "en", "English"));
        ed4.setPattern(patternCC);

        Artifact artifact = new Artifact();
        artifact.setType(ArtifactType.RESOURCE);
        artifact.setName("test-sd");
        artifact.setContent(fhirContext.newJsonParser().encodeResourceToString(sd).getBytes());

        when(artifactRepository.findAll()).thenReturn(List.of(artifact));

        Set<String> dependencies = artifactService.getTerminologyDependencies();

        assertTrue(dependencies.contains("http://hl7.org/fhir/ValueSet/administrative-gender|4.0.1"));
        assertTrue(dependencies.contains("http://example.org/CodeSystem/test|1.2.3"));
        assertTrue(dependencies.contains("http://hl7.org/fhir/ValueSet/marital-status"));
        assertTrue(dependencies.contains("http://example.org/CodeSystem/lang"));
    }

    @Test
    void getTerminologyDependencies_Empty() throws IOException {
        when(artifactRepository.findAll()).thenReturn(List.of());
        Set<String> dependencies = artifactService.getTerminologyDependencies();
        assertTrue(dependencies.isEmpty());
    }

    @Test
    void getTerminologyDependencies_NoSD() throws IOException {
        ValueSet vs = new ValueSet();
        vs.setUrl("http://example.org/ValueSet/test");

        Artifact artifact = new Artifact();
        artifact.setType(ArtifactType.RESOURCE);
        artifact.setName("test-vs");
        artifact.setContent(fhirContext.newJsonParser().encodeResourceToString(vs).getBytes());

        when(artifactRepository.findAll()).thenReturn(List.of(artifact));

        Set<String> dependencies = artifactService.getTerminologyDependencies();
        assertTrue(dependencies.isEmpty());
    }
}
