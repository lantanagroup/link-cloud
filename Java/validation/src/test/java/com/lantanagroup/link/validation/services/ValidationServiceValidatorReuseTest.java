package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.validation.FhirValidator;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.*;

/**
 * Verifies that {@link ValidationService} reuses one validator (and its support-chain cache) across calls, and
 * rebuilds it only when {@link ArtifactService} invalidates its artifact support. See LEGLINK-909.
 */
class ValidationServiceValidatorReuseTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final String PROFILE_URL = "http://example.org/fhir/StructureDefinition/leglink-909-observation";

    private ArtifactRepository artifactRepository;
    private ArtifactService artifactService;
    private ValidationCacheService validationCacheService;
    private LinkConfig linkConfig;
    private ExecutorService bundleValidationExecutor;

    @BeforeEach
    void setUp() {
        artifactRepository = mock(ArtifactRepository.class);
        when(artifactRepository.findAll()).thenReturn(List.of());

        linkConfig = mock(LinkConfig.class);
        when(linkConfig.getTerminologyServiceUrl()).thenReturn("http://link-terminology:8076");
        when(linkConfig.getWhiteListCodeSystemRegex()).thenReturn(new ArrayList<>());
        when(linkConfig.getWhiteListValueSetRegex()).thenReturn(new ArrayList<>());

        artifactService = spy(new ArtifactService(FHIR_CONTEXT, artifactRepository, linkConfig));

        // Every remote terminology call routes through this mock, so its invocations stand in for the
        // GET CodeSystem?url= / GET ValueSet?url= searches. Defaults (false/null) make the chain fall
        // through to the in-memory supports.
        validationCacheService = mock(ValidationCacheService.class);

        bundleValidationExecutor = Executors.newFixedThreadPool(2);
    }

    @AfterEach
    void tearDown() {
        bundleValidationExecutor.shutdownNow();
    }

    private ValidationService newService() {
        LinkConfig ignoreConfig = mock(LinkConfig.class);
        return new ValidationService(
                FHIR_CONTEXT,
                artifactService,
                linkConfig,
                validationCacheService,
                new ValidationResultIgnoreService(ignoreConfig),
                bundleValidationExecutor);
    }

    @Test
    void chainCacheConfiguration_isSetExplicitly() {
        var config = ValidationService.chainCacheConfiguration();
        assertEquals(ValidationService.CHAIN_CACHE_SIZE, config.getCacheSize());
    }

    @Test
    void reusesValidatorAcrossCalls() throws Exception {
        ValidationService service = newService();

        FhirValidator first = service.getValidator();
        service.validate(terminologyBundle());
        FhirValidator second = service.getValidator();

        assertSame(first, second, "validator must be reused while artifacts are unchanged");
        verify(artifactService, times(1)).createValidationSupport();
    }

    @Test
    void secondValidationIssuesNoTerminologySupportLookups() {
        ValidationService service = newService();
        Bundle bundle = terminologyBundle();

        service.validate(bundle);
        int firstRunLookups = supportLookupCount();
        assertTrue(firstRunLookups > 0, "first run must consult the remote terminology support");

        clearInvocations(validationCacheService);
        service.validate(bundle);

        verify(validationCacheService, never()).cachedIsCodeSystemSupported(any(), anyString());
        verify(validationCacheService, never()).cachedIsValueSetSupported(any(), anyString());
        verify(validationCacheService, never()).cachedFetchCodeSystem(any(), anyString());
        verify(validationCacheService, never()).cachedFetchValueSet(any(), anyString());
    }

    @Test
    void rebuildsValidatorAfterArtifactUpload() throws Exception {
        ValidationService service = newService();
        Bundle bundle = profiledObservationBundle();

        FhirValidator before = service.getValidator();
        List<Result> beforeResults = service.validate(bundle);
        assertTrue(beforeResults.stream().anyMatch(r -> mentionsUnknownProfile(r.getMessage())),
                "profile should be unknown before upload: " + messages(beforeResults));

        byte[] content = FHIR_CONTEXT.newJsonParser().encodeResourceToString(profile()).getBytes(StandardCharsets.UTF_8);
        Artifact artifact = artifactService.getArtifact(ArtifactType.RESOURCE, "leglink-909-observation", content);
        when(artifactRepository.findAll()).thenReturn(List.of(artifact));
        artifactService.saveArtifact(ArtifactType.RESOURCE, "leglink-909-observation", content);

        List<Result> afterResults = service.validate(bundle);
        FhirValidator after = service.getValidator();

        assertNotSame(before, after, "validator must be rebuilt after an artifact change");
        assertFalse(afterResults.stream().anyMatch(r -> mentionsUnknownProfile(r.getMessage())),
                "profile should be known after upload: " + messages(afterResults));
        assertTrue(afterResults.stream().anyMatch(r -> r.getMessage() != null
                        && r.getMessage().contains("Observation.subject")
                        && r.getMessage().contains("minimum required")),
                "uploaded profile's subject constraint should now apply: " + messages(afterResults));
    }

    @Test
    void concurrentValidationMatchesSequentialFreshValidators() throws Exception {
        List<Bundle> bundles = concurrencyBundles();

        // Baseline: a fresh service per bundle, equivalent to the previous per-call construction.
        List<List<String>> expected = new ArrayList<>();
        for (Bundle bundle : bundles) {
            expected.add(normalize(newService().validate(bundle)));
        }

        ValidationService shared = newService();
        ExecutorService callers = Executors.newFixedThreadPool(8);
        try {
            List<Future<List<String>>> futures = new ArrayList<>();
            List<Integer> bundleIndexes = new ArrayList<>();
            for (int round = 0; round < 5; round++) {
                for (int i = 0; i < bundles.size(); i++) {
                    Bundle bundle = bundles.get(i);
                    futures.add(callers.submit(() -> normalize(shared.validate(bundle))));
                    bundleIndexes.add(i);
                }
            }
            for (int f = 0; f < futures.size(); f++) {
                int index = bundleIndexes.get(f);
                assertEquals(expected.get(index), futures.get(f).get(),
                        "concurrent result for bundle " + index + " differs from the fresh-validator baseline");
            }
        } finally {
            callers.shutdownNow();
        }
    }

    private int supportLookupCount() {
        return (int) mockingDetails(validationCacheService).getInvocations().stream()
                .filter(i -> switch (i.getMethod().getName()) {
                    case "cachedIsCodeSystemSupported", "cachedIsValueSetSupported",
                         "cachedFetchCodeSystem", "cachedFetchValueSet" -> true;
                    default -> false;
                })
                .count();
    }

    private static boolean mentionsUnknownProfile(String message) {
        return message != null && message.contains(PROFILE_URL) && message.contains("could not be found");
    }

    private static List<String> messages(List<Result> results) {
        return results.stream().map(Result::getMessage).toList();
    }

    // Concurrent bundle validation does not guarantee message order, so compare as a sorted multiset.
    private static List<String> normalize(List<Result> results) {
        return results.stream()
                .map(r -> r.getSeverity() + "|" + r.getExpression() + "|" + r.getLocation() + "|" + r.getMessage())
                .sorted()
                .toList();
    }

    private static Bundle bundleOf(Resource... resources) {
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        int i = 0;
        for (Resource resource : resources) {
            resource.setId(resource.fhirType() + "-" + i);
            bundle.addEntry().setFullUrl("http://example.org/fhir/" + resource.fhirType() + "/" + resource.fhirType() + "-" + i++)
                    .setResource(resource);
        }
        return bundle;
    }

    private static Observation loincObservation() {
        Observation observation = new Observation();
        observation.setStatus(Observation.ObservationStatus.FINAL);
        observation.getCode().addCoding(new Coding("http://loinc.org", "8867-4", "Heart rate"));
        return observation;
    }

    private static Bundle terminologyBundle() {
        Patient patient = new Patient();
        patient.setGender(Enumerations.AdministrativeGender.FEMALE);
        patient.getMaritalStatus().addCoding(
                new Coding("http://terminology.hl7.org/CodeSystem/v3-MaritalStatus", "M", "Married"));
        return bundleOf(patient, loincObservation());
    }

    private static Bundle profiledObservationBundle() {
        Observation observation = loincObservation();
        observation.getMeta().addProfile(PROFILE_URL);
        return bundleOf(observation);
    }

    private static StructureDefinition profile() {
        StructureDefinition sd = new StructureDefinition();
        sd.setUrl(PROFILE_URL);
        sd.setName("Leglink909Observation");
        sd.setStatus(Enumerations.PublicationStatus.ACTIVE);
        sd.setFhirVersion(Enumerations.FHIRVersion._4_0_1);
        sd.setKind(StructureDefinition.StructureDefinitionKind.RESOURCE);
        sd.setAbstract(false);
        sd.setType("Observation");
        sd.setBaseDefinition("http://hl7.org/fhir/StructureDefinition/Observation");
        sd.setDerivation(StructureDefinition.TypeDerivationRule.CONSTRAINT);
        sd.getDifferential().addElement().setPath("Observation").setId("Observation");
        sd.getDifferential().addElement().setPath("Observation.subject").setMin(1).setId("Observation.subject");
        return sd;
    }

    private static List<Bundle> concurrencyBundles() {
        Patient validPatient = new Patient();
        validPatient.setGender(Enumerations.AdministrativeGender.MALE);
        validPatient.setBirthDateElement(new DateType("1980-01-01"));

        Observation missingStatus = loincObservation();
        missingStatus.setStatus(null);

        Observation badUcum = loincObservation();
        badUcum.setValue(new Quantity().setValue(72).setSystem("http://unitsofmeasure.org").setCode("not-a-unit"));

        Encounter missingClassAndStatus = new Encounter();

        Condition missingSubject = new Condition();
        missingSubject.getCode().addCoding(new Coding("http://snomed.info/sct", "38341003", "Hypertension"));

        return List.of(
                bundleOf(validPatient),
                bundleOf(missingStatus),
                bundleOf(badUcum),
                bundleOf(missingClassAndStatus),
                bundleOf(missingSubject),
                terminologyBundle());
    }
}
