package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.models.PackageDetailsModel;
import com.lantanagroup.link.validation.models.TerminologyDependency;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
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
import static org.mockito.Mockito.*;

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
        artifactService = spy(new ArtifactService(fhirContext, artifactRepository, linkConfig));
    }

    @Test
    void getTerminologyDependencies() throws IOException {
        StructureDefinition sd = new StructureDefinition();
        sd.setUrl("http://example.org/StructureDefinition/test");

        // Snapshot with binding - exists in artifacts
        ElementDefinition ed = sd.getSnapshot().addElement();
        ed.setPath("Patient.gender");
        ed.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);
        ed.getBinding().setValueSet("http://example.org/ValueSet/administrative-gender|4.0.1");

        // Snapshot with binding (no version) - exists in artifacts
        ElementDefinition ed2 = sd.getSnapshot().addElement();
        ed2.setPath("Patient.maritalStatus");
        ed2.getBinding().setStrength(Enumerations.BindingStrength.EXTENSIBLE);
        ed2.getBinding().setValueSet("http://example.org/ValueSet/marital-status");

        // Snapshot with binding (no version) - does NOT exist
        ElementDefinition ed3 = sd.getSnapshot().addElement();
        ed3.setPath("Patient.active");
        ed3.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);
        ed3.getBinding().setValueSet("http://example.org/ValueSet/non-existent");

        // Snapshot with fixed Coding (with version) - ignored
        ElementDefinition ed4 = sd.getSnapshot().addElement();
        ed4.setPath("Patient.extension");
        Coding fixedCoding = new Coding("http://example.org/CodeSystem/test", "code1", "Code 1");
        fixedCoding.setVersion("1.2.3");
        ed4.setFixed(fixedCoding);

        // Snapshot with pattern CodeableConcept - ignored
        ElementDefinition ed5 = sd.getSnapshot().addElement();
        ed5.setPath("Patient.communication.language");
        CodeableConcept patternCC = new CodeableConcept();
        patternCC.addCoding(new Coding("http://example.org/CodeSystem/lang", "en", "English"));
        ed5.setPattern(patternCC);

        // Snapshot with binding (set to a version) - value set found but version doesn't match
        ElementDefinition ed6 = sd.getSnapshot().addElement();
        ed6.setPath("Patient.communication.language");
        ed6.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);
        ed6.getBinding().setValueSet("http://example.org/ValueSet/patient-communication|1.2.3");

        // Snapshot with fixedUri on .system path - included
        ElementDefinition ed7 = sd.getSnapshot().addElement();
        ed7.setPath("Patient.extension.system");
        ed7.setFixed(new UriType("http://example.org/CodeSystem/test-cs"));

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

        // Add ValueSet that exists (no version)
        ValueSet vs3 = new ValueSet();
        vs3.setUrl("http://example.org/ValueSet/patient-communication");
        vs3.setVersion("3.2.1");
        Artifact artifactVs3 = new Artifact();
        artifactVs3.setType(ArtifactType.RESOURCE);
        artifactVs3.setName("vs3");
        artifactVs3.setContent(fhirContext.newJsonParser().encodeResourceToString(vs3).getBytes());

        when(artifactRepository.findAll()).thenReturn(List.of(artifactSd, artifactVs1, artifactVs2, artifactVs3));

        List<TerminologyDependency> dependencies = artifactService.getTerminologyDependencies();

        assertEquals(5, dependencies.size());

        TerminologyDependency dep1 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/ValueSet/administrative-gender")).findFirst().orElseThrow();
        assertEquals("4.0.1", dep1.getVersion());
        assertTrue(dep1.isResourceExists());
        assertTrue(dep1.isVersionExists());

        TerminologyDependency dep2 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/ValueSet/marital-status")).findFirst().orElseThrow();
        assertNull(dep2.getVersion());
        assertTrue(dep2.isResourceExists());
        assertTrue(dep2.isVersionExists()); // version specified was null, so versionExists is true

        TerminologyDependency dep3 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/ValueSet/non-existent")).findFirst().orElseThrow();
        assertNull(dep3.getVersion());
        assertFalse(dep3.isResourceExists());
        assertTrue(dep3.isVersionExists());

        TerminologyDependency dep4 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/ValueSet/patient-communication")).findFirst().orElseThrow();
        assertEquals("1.2.3", dep4.getVersion());
        assertTrue(dep4.isResourceExists());
        assertFalse(dep4.isVersionExists());

        TerminologyDependency dep5 = dependencies.stream().filter(d -> d.getUrl().equals("http://example.org/CodeSystem/test-cs")).findFirst().orElseThrow();
        assertNull(dep5.getVersion());
        assertFalse(dep5.isResourceExists());
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

    @Test
    void getPackageDetails() throws IOException {
        String packageId = "test-package";
        Artifact artifact = new Artifact();
        artifact.setType(ArtifactType.PACKAGE);
        artifact.setName(packageId);

        when(artifactRepository.findByTypeAndName(ArtifactType.PACKAGE, packageId)).thenReturn(Optional.of(artifact));

        ArtifactValidationSupport packageSupport = mock(ArtifactValidationSupport.class);
        doReturn(packageSupport).when(artifactService).createValidationSupport();

        ImplementationGuide ig = new ImplementationGuide();
        ig.setVersion("1.0.0");
        when(packageSupport.getImplementationGuides()).thenReturn(List.of(ig));

        StructureDefinition sd = new StructureDefinition();
        sd.setId("test-sd");
        sd.setUrl("http://example.org/sd");
        sd.setName("TestSD");
        sd.setVersion("0.1.0");

        ValueSet vs = new ValueSet();
        vs.setId("test-vs");
        vs.setUrl("http://example.org/vs");
        vs.setName("TestVS");
        vs.setVersion("0.2.0");

        CodeSystem cs = new CodeSystem();
        cs.setId("test-cs");
        cs.setUrl("http://example.org/cs");
        cs.setName("TestCS");
        cs.setVersion("0.3.0");

        when(packageSupport.fetchAllConformanceResources()).thenReturn(List.of(sd, vs, cs));

        PackageDetailsModel details = artifactService.getPackageDetails(packageId);

        assertNotNull(details);
        assertEquals("1.0.0", details.getVersion());
        assertEquals(3, details.getResources().size());

        // Sorted by type: CodeSystem, StructureDefinition, ValueSet
        assertEquals("CodeSystem", details.getResources().get(0).getResourceType());
        assertEquals("test-cs", details.getResources().get(0).getId());

        assertEquals("StructureDefinition", details.getResources().get(1).getResourceType());
        assertEquals("test-sd", details.getResources().get(1).getId());

        assertEquals("ValueSet", details.getResources().get(2).getResourceType());
        assertEquals("test-vs", details.getResources().get(2).getId());
    }

    @Test
    void getPackageDetails_NotFound() throws IOException {
        when(artifactRepository.findByTypeAndName(ArtifactType.PACKAGE, "non-existent")).thenReturn(Optional.empty());
        PackageDetailsModel results = artifactService.getPackageDetails("non-existent");
        assertNull(results);
    }
}
