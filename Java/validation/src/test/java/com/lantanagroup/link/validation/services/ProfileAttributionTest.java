package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.SingleValidationMessage;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.hl7.fhir.common.hapi.validation.support.SnapshotGeneratingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Verifies that each validation finding is attributed to the profile that produced it, without changing anything
 * else HAPI reports, and independently of how many threads validate bundle entries. See LEGLINK-1172.
 */
class ProfileAttributionTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final String VERSION = "1.0.0";
    private static final String LAB_URL = "http://example.org/fhir/StructureDefinition/leglink-1172-observation-lab";
    private static final String VITALS_URL = "http://example.org/fhir/StructureDefinition/leglink-1172-observation-vitals";
    private static final String LAB_PROFILE = LAB_URL + "|" + VERSION;
    private static final String VITALS_PROFILE = VITALS_URL + "|" + VERSION;

    private ArtifactService artifactService;
    private final List<ExecutorService> executors = new ArrayList<>();

    @BeforeEach
    void setUp() throws Exception {
        LinkConfig linkConfig = mock(LinkConfig.class);
        when(linkConfig.getWhiteListCodeSystemRegex()).thenReturn(new ArrayList<>());
        when(linkConfig.getWhiteListValueSetRegex()).thenReturn(new ArrayList<>());

        List<Artifact> artifacts = new ArrayList<>();
        ArtifactRepository artifactRepository = mock(ArtifactRepository.class);
        when(artifactRepository.findAll()).thenReturn(artifacts);
        artifactService = new ArtifactService(FHIR_CONTEXT, artifactRepository, linkConfig);

        // The lab profile requires interpretation; the vitals profile requires subject. A finding for either is
        // therefore attributable to exactly one of them.
        for (StructureDefinition profile : List.of(
                profile(LAB_URL, "Leglink1172ObservationLab", "Observation.interpretation"),
                profile(VITALS_URL, "Leglink1172ObservationVitals", "Observation.subject"))) {
            String name = profile.getName();
            byte[] content = FHIR_CONTEXT.newJsonParser().encodeResourceToString(profile).getBytes(StandardCharsets.UTF_8);
            artifacts.add(artifactService.getArtifact(ArtifactType.RESOURCE, name, content));
            artifactService.saveArtifact(ArtifactType.RESOURCE, name, content);
        }
    }

    @AfterEach
    void tearDown() {
        executors.forEach(ExecutorService::shutdownNow);
    }

    private ValidationService newService(int parallelism) {
        ExecutorService executor = Executors.newFixedThreadPool(parallelism);
        executors.add(executor);
        LinkConfig linkConfig = mock(LinkConfig.class);
        return new ValidationService(
                FHIR_CONTEXT,
                artifactService,
                linkConfig,
                mock(ValidationCacheService.class),
                new ValidationResultIgnoreService(linkConfig),
                executor);
    }

    @Test
    void validator_keepsConcurrentBundleValidationOn() throws Exception {
        // Profile attribution is only accurate when each entry is validated standalone. With the flag off, base-level
        // findings on a profiled entry are attributed to the core StructureDefinition instead (and expression changes
        // format entirely).
        assertTrue(newService(1).getValidator().isConcurrentBundleValidation());
    }

    @Test
    void findingsAreAttributedToTheirOwnProfile() {
        List<Result> results = newService(4).validate(bundle());

        assertEquals(List.of(LAB_PROFILE), profilesAt(results, 0, "Observation.interpretation"),
                "lab Observation: " + describe(results));
        assertEquals(List.of(VITALS_PROFILE), profilesAt(results, 1, "Observation.subject"),
                "vitals Observation: " + describe(results));
        assertEquals(List.of(LAB_PROFILE), profilesAt(results, 2, "Observation.interpretation"),
                "dual-profile Observation, lab finding: " + describe(results));
        assertEquals(List.of(VITALS_PROFILE), profilesAt(results, 2, "Observation.subject"),
                "dual-profile Observation, vitals finding: " + describe(results));
        // A resource declaring no profile is validated against the core StructureDefinition, and is attributed to it.
        assertEquals(List.of("http://hl7.org/fhir/StructureDefinition/Observation|4.0.1"),
                profilesAt(results, 3, "Observation.status"),
                "unprofiled Observation: " + describe(results));
    }

    @Test
    void bestPracticeFindingsHaveNoProfile() {
        List<Result> bestPractice = newService(4).validate(bundle()).stream()
                .filter(r -> r.getMessage() != null && r.getMessage().startsWith("Best Practice Recommendation"))
                .toList();

        assertFalse(bestPractice.isEmpty());
        assertTrue(bestPractice.stream().allMatch(r -> r.getProfile() == null), describe(bestPractice));
    }

    @Test
    void profileIsOneOfTheEntrysDeclaredProfilesOrNull() {
        Bundle bundle = bundle();
        for (Result result : newService(4).validate(bundle)) {
            if (result.getProfile() == null) {
                continue;
            }
            int index = entryIndex(result);
            List<String> declared = bundle.getEntry().get(index).getResource().getMeta().getProfile().stream()
                    .map(CanonicalType::getValue)
                    .toList();
            String url = result.getProfile().replaceFirst("\\|.*$", "");
            assertTrue(declared.contains(url) || url.startsWith("http://hl7.org/fhir/StructureDefinition/"),
                    "entry " + index + " declares " + declared + " but finding was attributed to "
                            + result.getProfile() + ": " + result.getMessage());
        }
    }

    @Test
    void messagesAreIdenticalToHapisOwnConversion() throws Exception {
        Bundle bundle = bundle();
        FhirValidator validator = newService(4).getValidator();
        FhirValidator baseline = baselineValidator(4);

        List<SingleValidationMessage> expected = baseline.validateWithResult(bundle).getMessages();
        List<SingleValidationMessage> actual = validator.validateWithResult(bundle).getMessages();

        assertEquals(expected.stream().map(ProfileAttributionTest::fields).toList(),
                actual.stream().map(ProfileAttributionTest::fields).toList());
        assertTrue(actual.stream().allMatch(ProfileAttributedValidationMessage.class::isInstance),
                "every message must survive HAPI's bundle-entry prefixing as a ProfileAttributedValidationMessage");
    }

    @Test
    void attributionIsIndependentOfParallelism() {
        Bundle bundle = bundle();
        List<String> sequential = attributed(newService(1).validate(bundle));
        assertFalse(sequential.isEmpty());

        ValidationService parallel = newService(16);
        for (int round = 0; round < 5; round++) {
            assertEquals(sequential, attributed(parallel.validate(bundle)), "round " + round);
        }
    }

    // A baseline with HAPI's stock FhirInstanceValidator over the same support chain ValidationService builds when no
    // remote terminology service is configured.
    private FhirValidator baselineValidator(int parallelism) throws Exception {
        ValidationSupportChain chain = new ValidationSupportChain(
                ValidationService.chainCacheConfiguration(),
                new DefaultProfileValidationSupport(FHIR_CONTEXT),
                artifactService.getValidationSupport(),
                new SnapshotGeneratingValidationSupport(FHIR_CONTEXT));
        ValidationService.loadTerminologyValidationSupport(
                FHIR_CONTEXT, mock(LinkConfig.class), chain, mock(ValidationCacheService.class));
        ExecutorService executor = Executors.newFixedThreadPool(parallelism);
        executors.add(executor);
        FhirValidator validator = new FhirValidator(FHIR_CONTEXT);
        validator.registerValidatorModule(new FhirInstanceValidator(chain));
        validator.setConcurrentBundleValidation(true);
        validator.setExecutorService(executor);
        return validator;
    }

    private static String fields(SingleValidationMessage message) {
        return String.join("|",
                String.valueOf(message.getSeverity()),
                String.valueOf(message.getMessageId()),
                String.valueOf(message.getLocationLine()),
                String.valueOf(message.getLocationCol()),
                String.valueOf(message.getLocationString()),
                String.valueOf(message.getMessage()),
                String.valueOf(message.getSliceMessages()));
    }

    private static List<String> attributed(List<Result> results) {
        return results.stream()
                .map(r -> r.getSeverity() + "|" + r.getCode() + "|" + r.getExpression() + "|" + r.getLocation()
                        + "|" + r.getMessage() + "|" + r.getProfile())
                .toList();
    }

    private static List<String> profilesAt(List<Result> results, int entryIndex, String element) {
        return results.stream()
                .filter(r -> entryIndex(r) == entryIndex)
                .filter(r -> r.getMessage() != null && r.getMessage().contains(element)
                        && r.getMessage().contains("minimum required"))
                .map(Result::getProfile)
                .toList();
    }

    private static int entryIndex(Result result) {
        String expression = result.getExpression();
        int start = expression.indexOf('[') + 1;
        return Integer.parseInt(expression.substring(start, expression.indexOf(']', start)));
    }

    private static String describe(List<Result> results) {
        return results.stream()
                .map(r -> r.getExpression() + " [" + r.getProfile() + "] " + r.getMessage())
                .toList()
                .toString();
    }

    private static Observation observation(String... profiles) {
        Observation observation = new Observation();
        observation.setStatus(Observation.ObservationStatus.FINAL);
        observation.getCode().addCoding(new Coding("http://loinc.org", "8867-4", "Heart rate"));
        for (String profile : profiles) {
            observation.getMeta().addProfile(profile);
        }
        return observation;
    }

    private static Bundle bundle() {
        Observation unprofiled = observation();
        unprofiled.setStatus(null);
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        int i = 0;
        for (Resource resource : List.of(
                observation(LAB_URL),
                observation(VITALS_URL),
                observation(LAB_URL, VITALS_URL),
                unprofiled)) {
            resource.setId("observation-" + i);
            bundle.addEntry().setFullUrl("http://example.org/fhir/Observation/observation-" + i++).setResource(resource);
        }
        return bundle;
    }

    private static StructureDefinition profile(String url, String name, String requiredPath) {
        StructureDefinition sd = new StructureDefinition();
        sd.setUrl(url);
        sd.setVersion(VERSION);
        sd.setName(name);
        sd.setStatus(Enumerations.PublicationStatus.ACTIVE);
        sd.setFhirVersion(Enumerations.FHIRVersion._4_0_1);
        sd.setKind(StructureDefinition.StructureDefinitionKind.RESOURCE);
        sd.setAbstract(false);
        sd.setType("Observation");
        sd.setBaseDefinition("http://hl7.org/fhir/StructureDefinition/Observation");
        sd.setDerivation(StructureDefinition.TypeDerivationRule.CONSTRAINT);
        sd.getDifferential().addElement().setPath("Observation").setId("Observation");
        sd.getDifferential().addElement().setPath(requiredPath).setMin(1).setId(requiredPath);
        return sd;
    }
}
