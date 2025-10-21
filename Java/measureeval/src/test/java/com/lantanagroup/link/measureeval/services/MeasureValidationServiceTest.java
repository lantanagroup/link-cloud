package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.models.RelatedArtifactInfo;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.HttpStatus;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class MeasureValidationServiceTest {

    @Mock
    private MeasureDefinitionRepository repository;

    @InjectMocks
    private MeasureValidationService service;

    private final String measureId = "test-measure";

    @Test
    void getRelatedArtifacts_NotFound_ThrowsException() {
        when(repository.findById(measureId)).thenReturn(Optional.empty());

        ResponseStatusException exception = assertThrows(ResponseStatusException.class, () -> {
            service.getRelatedArtifacts(measureId);
        });

        assertEquals(HttpStatus.NOT_FOUND, exception.getStatusCode());
        assertEquals("Measure definition not found", exception.getReason());
    }

    @Test
    void getRelatedArtifacts_NoBundle_ThrowsException() {
        MeasureDefinition measureDefinition = new MeasureDefinition();
        measureDefinition.setId(measureId);
        measureDefinition.setBundle(null);

        when(repository.findById(measureId)).thenReturn(Optional.of(measureDefinition));

        ResponseStatusException exception = assertThrows(ResponseStatusException.class, () -> {
            service.getRelatedArtifacts(measureId);
        });

        assertEquals(HttpStatus.BAD_REQUEST, exception.getStatusCode());
        assertEquals("Measure definition does not contain a bundle", exception.getReason());
    }

    @Test
    void getRelatedArtifacts_Success() {
        // Setup Bundle
        Bundle bundle = new Bundle();

        // Library 1
        Library lib1 = new Library();
        lib1.setUrl("http://example.org/Library/lib1");
        lib1.setName("Lib1");
        lib1.setVersion("1.0.0");
        // Dependency found in bundle
        lib1.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://example.org/Library/dependency1|1.0.0");
        // Dependency NOT found in bundle
        lib1.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://example.org/Library/dependency2");
        // Ignored dependency
        lib1.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://fhir.org/guides/cqf/something");
        // Not a DEPENDSON type
        lib1.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.CITATION)
                .setResource("http://example.org/Library/citation1");

        // Library 2 (uses same dependency)
        Library lib2 = new Library();
        lib2.setUrl("http://example.org/Library/lib2");
        lib2.setName("Lib2");
        lib2.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://example.org/Library/dependency1|1.0.0");

        // Dependency resource (present in bundle)
        Library dep1 = new Library();
        dep1.setUrl("http://example.org/Library/dependency1");
        dep1.setVersion("1.0.0");

        bundle.addEntry().setResource(lib1);
        bundle.addEntry().setResource(lib2);
        bundle.addEntry().setResource(dep1);

        MeasureDefinition measureDefinition = new MeasureDefinition();
        measureDefinition.setId(measureId);
        measureDefinition.setBundle(bundle);

        when(repository.findById(measureId)).thenReturn(Optional.of(measureDefinition));

        // Act
        List<RelatedArtifactInfo> results = service.getRelatedArtifacts(measureId);

        // Assert
        assertNotNull(results);
        assertEquals(2, results.size());

        // Check dependency1
        RelatedArtifactInfo info1 = results.stream()
                .filter(i -> "http://example.org/Library/dependency1".equals(i.getUrl()))
                .findFirst().orElseThrow();
        assertEquals("1.0.0", info1.getVersion());
        assertTrue(info1.isFound());
        assertEquals(2, info1.getLibraries().size());
        assertTrue(info1.getLibraries().containsKey("http://example.org/Library/lib1"));
        assertTrue(info1.getLibraries().containsKey("http://example.org/Library/lib2"));

        // Check dependency2
        RelatedArtifactInfo info2 = results.stream()
                .filter(i -> "http://example.org/Library/dependency2".equals(i.getUrl()))
                .findFirst().orElseThrow();
        assertNull(info2.getVersion());
        assertFalse(info2.isFound());
        assertEquals(1, info2.getLibraries().size());
        assertTrue(info2.getLibraries().containsKey("http://example.org/Library/lib1"));
    }

    @Test
    void getRelatedArtifacts_HandlesNonMetadataResource() {
        Bundle bundle = new Bundle();

        Library lib = new Library();
        lib.setUrl("http://example.org/Library/lib");
        lib.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://example.org/Patient/123");

        // Add a non-metadata resource to the bundle (e.g., a Patient)
        Patient patient = new Patient();
        patient.setId("123");
        // Patient doesn't have a canonical URL in R4 (it's not a MetadataResource)

        bundle.addEntry().setResource(lib);
        bundle.addEntry().setResource(patient);

        MeasureDefinition measureDefinition = new MeasureDefinition();
        measureDefinition.setId(measureId);
        measureDefinition.setBundle(bundle);

        when(repository.findById(measureId)).thenReturn(Optional.of(measureDefinition));

        // Act
        List<RelatedArtifactInfo> results = service.getRelatedArtifacts(measureId);

        // Assert
        assertEquals(1, results.size());
        assertFalse(results.get(0).isFound()); // Patient is not found by canonical URL
    }

    @Test
    void getRelatedArtifacts_IgnoresArtifactsStartingWithCqfUrl() {
        Bundle bundle = new Bundle();

        Library lib = new Library();
        lib.setUrl("http://example.org/Library/lib");
        lib.setName("TestLib");

        // Add ignored artifact with http://fhir.org/guides/cqf/ prefix
        lib.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://fhir.org/guides/cqf/common/Library/FHIRHelpers");

        // Add another ignored artifact with http://fhir.org/guides/cqf/ prefix
        lib.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://fhir.org/guides/cqf/us/something/Library/Example");

        // Add a non-ignored artifact
        lib.addRelatedArtifact()
                .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
                .setResource("http://example.org/Library/dependency");

        bundle.addEntry().setResource(lib);

        MeasureDefinition measureDefinition = new MeasureDefinition();
        measureDefinition.setId(measureId);
        measureDefinition.setBundle(bundle);

        when(repository.findById(measureId)).thenReturn(Optional.of(measureDefinition));

        // Act
        List<RelatedArtifactInfo> results = service.getRelatedArtifacts(measureId);

        // Assert
        assertEquals(1, results.size());
        assertEquals("http://example.org/Library/dependency", results.get(0).getUrl());
        assertFalse(results.stream().anyMatch(r -> r.getUrl().startsWith("http://fhir.org/guides/cqf/")));
    }
}
