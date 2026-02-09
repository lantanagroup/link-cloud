package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.models.TerminologyDependency;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.hl7.fhir.r4.model.ElementDefinition;
import org.hl7.fhir.r4.model.Enumerations;
import org.hl7.fhir.r4.model.StructureDefinition;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.io.IOException;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class TerminologyDependencySourceProfileTest {
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
    void getTerminologyDependencies_SourceProfile() throws IOException {
        String vsUrl = "http://example.org/ValueSet/vs1";
        String profile1Url = "http://example.org/StructureDefinition/p1";
        String profile2Url = "http://example.org/StructureDefinition/p2";

        StructureDefinition p1 = new StructureDefinition();
        p1.setUrl(profile1Url);
        ElementDefinition e1_1 = p1.getSnapshot().addElement();
        e1_1.setPath("Patient.gender");
        e1_1.getBinding().setValueSet(vsUrl);
        e1_1.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);
        ElementDefinition e1_2 = p1.getSnapshot().addElement();
        e1_2.setPath("Patient.contact.gender");
        e1_2.getBinding().setValueSet(vsUrl);
        e1_2.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);

        StructureDefinition p2 = new StructureDefinition();
        p2.setUrl(profile2Url);
        ElementDefinition e2_1 = p2.getSnapshot().addElement();
        e2_1.setPath("Observation.code");
        e2_1.getBinding().setValueSet(vsUrl);
        e2_1.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);

        Artifact a1 = new Artifact();
        a1.setType(ArtifactType.RESOURCE);
        a1.setName("p1");
        a1.setContent(fhirContext.newJsonParser().encodeResourceToString(p1).getBytes());

        Artifact a2 = new Artifact();
        a2.setType(ArtifactType.RESOURCE);
        a2.setName("p2");
        a2.setContent(fhirContext.newJsonParser().encodeResourceToString(p2).getBytes());

        when(artifactRepository.findAll()).thenReturn(List.of(a1, a2));

        List<TerminologyDependency> dependencies = artifactService.getTerminologyDependencies();

        assertEquals(1, dependencies.size());
        TerminologyDependency dep = dependencies.get(0);
        assertEquals(vsUrl, dep.getUrl());
        assertEquals(2, dep.getSourceProfile().size());

        TerminologyDependency.SourceProfile sp1 = dep.getSourceProfile().stream()
                .filter(s -> s.getUrl().equals(profile1Url))
                .findFirst().orElseThrow();
        assertEquals(2, sp1.getElement().size());
        assertTrue(sp1.getElement().contains("Patient.gender"));
        assertTrue(sp1.getElement().contains("Patient.contact.gender"));

        TerminologyDependency.SourceProfile sp2 = dep.getSourceProfile().stream()
                .filter(s -> s.getUrl().equals(profile2Url))
                .findFirst().orElseThrow();
        assertEquals(1, sp2.getElement().size());
        assertTrue(sp2.getElement().contains("Observation.code"));
    }

    @Test
    void getTerminologyDependencies_Sorted() throws IOException {
        StructureDefinition p = new StructureDefinition();
        p.setUrl("http://example.org/SD");
        ElementDefinition elementA = p.getSnapshot().addElement();
        elementA.setPath("P.a");
        elementA.getBinding().setValueSet("http://example.org/VS/B");
        elementA.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);
        ElementDefinition elementB = p.getSnapshot().addElement();
        elementB.setPath("P.b");
        elementB.getBinding().setValueSet("http://example.org/VS/A");
        elementB.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED);

        Artifact a = new Artifact();
        a.setType(ArtifactType.RESOURCE);
        a.setName("p");
        a.setContent(fhirContext.newJsonParser().encodeResourceToString(p).getBytes());

        when(artifactRepository.findAll()).thenReturn(List.of(a));

        List<TerminologyDependency> dependencies = artifactService.getTerminologyDependencies();

        assertEquals(2, dependencies.size());
        assertEquals("http://example.org/VS/A", dependencies.get(0).getUrl());
        assertEquals("http://example.org/VS/B", dependencies.get(1).getUrl());
    }
}
