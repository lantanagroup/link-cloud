package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.models.TerminologyDependency;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.io.IOException;
import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class ArtifactServiceTest {
    private ArtifactService artifactService;

    @Mock
    private ArtifactRepository artifactRepository;

    @Mock
    private LinkConfig linkConfig;

    private final FhirContext fhirContext = FhirContext.forR4();

    @BeforeEach
    void setUp() {
        artifactService = new ArtifactService(fhirContext, artifactRepository, linkConfig);
    }

    @Test
    void getTerminologyDependencies() throws IOException {
        StructureDefinition sd = new StructureDefinition();
        sd.setUrl("http://example.org/StructureDefinition/test");

        // Snapshot with binding - exists in artifacts
        ElementDefinition ed = sd.getSnapshot().addElement();
        ed.setPath("Patient.gender");
        ed.getBinding().setValueSet("http://example.org/ValueSet/administrative-gender|4.0.1");

        // Snapshot with fixed Coding (with version) - does not exist
        ElementDefinition ed2 = sd.getSnapshot().addElement();
        ed2.setPath("Patient.extension");
        Coding fixedCoding = new Coding("http://example.org/CodeSystem/test", "code1", "Code 1");
        fixedCoding.setVersion("1.2.3");
        ed2.setFixed(fixedCoding);

        // Differential with binding (no version) - exists in artifacts
        ElementDefinition ed3 = sd.getDifferential().addElement();
        ed3.setPath("Patient.maritalStatus");
        ed3.getBinding().setValueSet("http://example.org/ValueSet/marital-status");

        // Differential with pattern CodeableConcept - exists but version mismatch (if we had version)
        ElementDefinition ed4 = sd.getDifferential().addElement();
        ed4.setPath("Patient.communication.language");
        CodeableConcept patternCC = new CodeableConcept();
        patternCC.addCoding(new Coding("http://example.org/CodeSystem/lang", "en", "English"));
        ed4.setPattern(patternCC);

        // Differential with binding (no version) - does NOT exist
        ElementDefinition ed5 = sd.getDifferential().addElement();
        ed5.setPath("Patient.active");
        ed5.getBinding().setValueSet("http://example.org/ValueSet/non-existent");

        Artifact artifactSd = new Artifact();
        artifactSd.setType(ArtifactType.RESOURCE);
        artifactSd.setName("test-sd");
        artifactSd.setContent(fhirContext.newJsonParser().encodeResourceToString(sd).getBytes());

        // Add ValueSet that exists
        ValueSet vs1 = new ValueSet();
        vs1.setUrl("http://example.org/ValueSet/administrative-gender");
        vs1.setVersion("4.0.1");
        Artifact artifactVs1 = new Artifact();
        artifactVs1.setType(ArtifactType.RESOURCE);
        artifactVs1.setName("vs1");
        artifactVs1.setContent(fhirContext.newJsonParser().encodeResourceToString(vs1).getBytes());

        // Add ValueSet that exists (no version)
        ValueSet vs2 = new ValueSet();
        vs2.setUrl("http://example.org/ValueSet/marital-status");
        Artifact artifactVs2 = new Artifact();
        artifactVs2.setType(ArtifactType.RESOURCE);
        artifactVs2.setName("vs2");
        artifactVs2.setContent(fhirContext.newJsonParser().encodeResourceToString(vs2).getBytes());

        // Add CodeSystem that exists
        CodeSystem cs1 = new CodeSystem();
        cs1.setUrl("http://example.org/CodeSystem/lang");
        Artifact artifactCs1 = new Artifact();
        artifactCs1.setType(ArtifactType.RESOURCE);
        artifactCs1.setName("cs1");
        artifactCs1.setContent(fhirContext.newJsonParser().encodeResourceToString(cs1).getBytes());

        when(artifactRepository.findAll()).thenReturn(List.of(artifactSd, artifactVs1, artifactVs2, artifactCs1));

        List<TerminologyDependency> dependencies = artifactService.getTerminologyDependencies();

        assertEquals(5, dependencies.size());

        TerminologyDependency dep1 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/ValueSet/administrative-gender")).findFirst().orElseThrow();
        assertEquals("4.0.1", dep1.getVersion());
        assertTrue(dep1.isResourceExists());
        assertTrue(dep1.isVersionExists());

        TerminologyDependency dep2 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/CodeSystem/test")).findFirst().orElseThrow();
        assertEquals("1.2.3", dep2.getVersion());
        assertFalse(dep2.isResourceExists());
        assertFalse(dep2.isVersionExists());

        TerminologyDependency dep3 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/ValueSet/marital-status")).findFirst().orElseThrow();
        assertNull(dep3.getVersion());
        assertTrue(dep3.isResourceExists());
        assertTrue(dep3.isVersionExists()); // version specified was null, so versionExists is true

        TerminologyDependency dep4 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/CodeSystem/lang")).findFirst().orElseThrow();
        assertNull(dep4.getVersion());
        assertTrue(dep4.isResourceExists());
        assertTrue(dep4.isVersionExists());

        TerminologyDependency dep5 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/ValueSet/non-existent")).findFirst().orElseThrow();
        assertNull(dep5.getVersion());
        assertFalse(dep5.isResourceExists());
        assertTrue(dep5.isVersionExists());
    }

    @Test
    void getTerminologyDependencies_Empty() throws IOException {
        when(artifactRepository.findAll()).thenReturn(List.of());
        List<TerminologyDependency> dependencies = artifactService.getTerminologyDependencies();
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

        List<TerminologyDependency> dependencies = artifactService.getTerminologyDependencies();
        assertTrue(dependencies.isEmpty());
    }

    @Test
    void getTerminologyDependencies_PackageNotFound() throws IOException {
        when(artifactRepository.findByTypeAndName(ArtifactType.PACKAGE, "non-existent")).thenReturn(Optional.empty());
        List<TerminologyDependency> results = artifactService.getTerminologyDependencies("non-existent");
        assertTrue(results.isEmpty());
    }
}
