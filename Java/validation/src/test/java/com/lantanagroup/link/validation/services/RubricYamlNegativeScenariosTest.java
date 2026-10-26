package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.exc.UnrecognizedPropertyException;
import com.fasterxml.jackson.dataformat.yaml.YAMLMapper;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.services.execution.executors.CustomCheckExecutor;
import jakarta.validation.ConstraintViolation;
import jakarta.validation.Validation;
import jakarta.validation.Validator;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Negative scenarios driven end-to-end from YAML source (the format the UI submits),
 * covering every register-time validation layer the same way the controller wires them:
 * strict YAML parsing -> bean validation -> domain validation. Each test's YAML document
 * is deliberately broken in exactly one way so the triggered rule is unambiguous.
 */
class RubricYamlNegativeScenariosTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final IFhirPath FHIR_PATH = FHIR_CONTEXT.newFhirPath();
    private static final Validator BEAN_VALIDATOR =
            Validation.buildDefaultValidatorFactory().getValidator();

    private final YAMLMapper strictYaml = new YAMLMapper();
    private CustomCheckExecutor customCheckExecutor;
    private RubricDefinitionValidator domainValidator;

    @BeforeEach
    void setUp() {
        strictYaml.enable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);
        customCheckExecutor = mock(CustomCheckExecutor.class);
        when(customCheckExecutor.canResolve(any(), any())).thenReturn(true);
        domainValidator = new RubricDefinitionValidator(customCheckExecutor, FHIR_PATH,
                new ScoringPolicyValidator(), new ApplicableContextValidator(FHIR_CONTEXT), 200);
    }

    /** A fully valid rubric in YAML; each test mutates one aspect via string replacement. */
    private static final String VALID_YAML = """
            id: piqi.demo
            semver: "1.0.0"
            title: Demo Rubric
            owner: qa-team
            dimensions:
              - CONFORMANCE
              - TERMINOLOGY
            scoringPolicy:
              type: piqi-dimension-scorecard
              rollup: worst-of
            applicableContext:
              fhirResources:
                - Bundle
              workflowTags:
                - submission
            checks:
              - id: name-exists
                type: FHIRPATH
                dimension: CONFORMANCE
                severityOverride: ERROR
                parameters:
                  expression: Patient.name.exists()
                ordinal: 0
                enabled: true
              - id: code-in-valueset
                type: VALUESET
                dimension: TERMINOLOGY
                severityOverride: WARNING
                parameters:
                  path: Observation.code
                  valueSet: http://hl7.org/fhir/ValueSet/observation-codes
                ordinal: 1
                enabled: true
            """;

    private RubricVersionPayloadDto parse(String yaml) {
        try {
            return strictYaml.readValue(yaml, RubricVersionPayloadDto.class);
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    private Set<String> beanViolations(String yaml) {
        return BEAN_VALIDATOR.validate(parse(yaml)).stream()
                .map(v -> v.getPropertyPath() + ": " + v.getMessage())
                .collect(Collectors.toSet());
    }

    private List<String> domainErrors(String yaml) {
        try {
            domainValidator.validate(parse(yaml));
        } catch (InvalidRubricDefinitionException e) {
            return e.getErrors();
        }
        throw new AssertionError("Expected the YAML to be rejected but it passed domain validation");
    }

    // ---- Sanity ---------------------------------------------------------------------------------

    @Test
    @DisplayName("baseline YAML passes every layer")
    void baselinePasses() {
        assertThat(beanViolations(VALID_YAML)).isEmpty();
        domainValidator.validate(parse(VALID_YAML));
    }


    @Test
    @DisplayName("YAML with every ordinal omitted passes every layer")
    void omittedOrdinalsPass() {
        String yaml = VALID_YAML.replace("    ordinal: 0\n", "").replace("    ordinal: 1\n", "");
        assertThat(yaml).doesNotContain("ordinal");
        assertThat(beanViolations(yaml)).isEmpty();
        domainValidator.validate(parse(yaml));
    }

    @Test
    @DisplayName("unknown top-level key (typo 'cheks') rejected by strict parsing")
    void unknownTopLevelKey() {
        String yaml = VALID_YAML.replace("checks:", "cheks:");
        assertThatThrownBy(() -> strictYaml.readValue(yaml, RubricVersionPayloadDto.class))
                .isInstanceOf(UnrecognizedPropertyException.class)
                .hasMessageContaining("cheks");
    }

    @Test
    @DisplayName("unknown key inside a check item rejected by strict parsing")
    void unknownCheckKey() {
        String yaml = VALID_YAML.replace("    enabled: true\n  - id: code-in-valueset",
                "    enabled: true\n    severtyOverride: ERROR\n  - id: code-in-valueset");
        assertThatThrownBy(() -> strictYaml.readValue(yaml, RubricVersionPayloadDto.class))
                .isInstanceOf(UnrecognizedPropertyException.class)
                .hasMessageContaining("severtyOverride");
    }

    @Test
    @DisplayName("dimension outside the PiqiDimension set rejected at parse time")
    void unknownDimensionValue() {
        String yaml = VALID_YAML.replace("- CONFORMANCE", "- NOT_A_DIMENSION");
        assertThatThrownBy(() -> strictYaml.readValue(yaml, RubricVersionPayloadDto.class))
                .hasMessageContaining("NOT_A_DIMENSION");
    }


    @Test
    @DisplayName("uppercase / underscore / too-short rubric id rejected")
    void badRubricId() {
        assertThat(beanViolations(VALID_YAML.replace("id: piqi.demo", "id: PIQI.Demo")))
                .anyMatch(v -> v.startsWith("id:"));
        assertThat(beanViolations(VALID_YAML.replace("id: piqi.demo", "id: piqi_demo")))
                .anyMatch(v -> v.startsWith("id:"));
        assertThat(beanViolations(VALID_YAML.replace("id: piqi.demo", "id: ab")))
                .anyMatch(v -> v.startsWith("id:"));
    }

    @Test
    @DisplayName("missing dimensions rejected")
    void missingDimensions() {
        String yaml = VALID_YAML.replace("dimensions:\n  - CONFORMANCE\n  - TERMINOLOGY\n", "");
        assertThat(beanViolations(yaml)).anyMatch(v -> v.startsWith("dimensions:"));
    }

    @Test
    @DisplayName("title over 256 chars rejected")
    void titleTooLong() {
        String yaml = VALID_YAML.replace("title: Demo Rubric", "title: " + "x".repeat(257));
        assertThat(beanViolations(yaml)).anyMatch(v -> v.startsWith("title:"));
    }

    @Test
    @DisplayName("owner over 128 chars rejected")
    void ownerTooLong() {
        String yaml = VALID_YAML.replace("owner: qa-team", "owner: " + "o".repeat(129));
        assertThat(beanViolations(yaml)).anyMatch(v -> v.startsWith("owner:"));
    }

    @Test
    @DisplayName("non-semver version rejected")
    void badSemver() {
        String yaml = VALID_YAML.replace("semver: \"1.0.0\"", "semver: \"v1\"");
        assertThat(beanViolations(yaml)).anyMatch(v -> v.startsWith("semver:"));
    }

    @Test
    @DisplayName("check id over 128 chars rejected")
    void checkIdTooLong() {
        String yaml = VALID_YAML.replace("- id: name-exists", "- id: " + "c".repeat(129));
        assertThat(beanViolations(yaml)).anyMatch(v -> v.contains(".id:"));
    }

    @Test
    @DisplayName("negative ordinal rejected")
    void negativeOrdinal() {
        String yaml = VALID_YAML.replace("ordinal: 0", "ordinal: -1");
        assertThat(beanViolations(yaml)).anyMatch(v -> v.contains("ordinal:"));
    }


    @Test
    @DisplayName("duplicate dimension rejected")
    void duplicateDimension() {
        String yaml = VALID_YAML.replace("- TERMINOLOGY", "- CONFORMANCE");
        // second CONFORMANCE makes TERMINOLOGY undeclared for the VALUESET check too
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("duplicate dimension CONFORMANCE"));
    }

    @Test
    @DisplayName("duplicate ordinal rejected")
    void duplicateOrdinal() {
        String yaml = VALID_YAML.replace("ordinal: 1", "ordinal: 0");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("duplicate ordinal 0"));
    }

    @Test
    @DisplayName("all checks disabled rejected")
    void allDisabled() {
        String yaml = VALID_YAML.replace("enabled: true", "enabled: false");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("at least one check must be enabled"));
    }

    @Test
    @DisplayName("invalid FHIRPath expression rejected")
    void invalidFhirPath() {
        String yaml = VALID_YAML.replace("expression: Patient.name.exists()",
                "expression: \"Patient.name.exists(\"");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("invalid FHIRPath expression"));
    }

    @Test
    @DisplayName("unknown parameter key (typo 'expresion') rejected")
    void unknownParameterKey() {
        String yaml = VALID_YAML.replace("expression: Patient.name.exists()",
                "expresion: Patient.name.exists()");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("unknown property 'expresion'"));
    }

    @Test
    @DisplayName("VALUESET with non-TERMINOLOGY dimension rejected")
    void valuesetWrongDimension() {
        String yaml = VALID_YAML.replace("    type: VALUESET\n    dimension: TERMINOLOGY",
                "    type: VALUESET\n    dimension: CONFORMANCE");
        assertThat(domainErrors(yaml))
                .anyMatch(e -> e.contains("type VALUESET must use dimension TERMINOLOGY, got CONFORMANCE"));
    }

    @Test
    @DisplayName("non-canonical valueSet URL rejected")
    void badValueSetUrl() {
        String yaml = VALID_YAML.replace("valueSet: http://hl7.org/fhir/ValueSet/observation-codes",
                "valueSet: not a url");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("valueSet: must be a canonical URL"));
    }

    @Test
    @DisplayName("finding code over 128 chars rejected")
    void codeTooLong() {
        String yaml = VALID_YAML.replace("expression: Patient.name.exists()",
                "expression: Patient.name.exists()\n      code: " + "c".repeat(129));
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("code: must be at most 128 characters"));
    }

    @Test
    @DisplayName("invalid scoringPolicy.type slug rejected")
    void badScoringPolicyType() {
        String yaml = VALID_YAML.replace("type: piqi-dimension-scorecard", "type: weighted-average");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("scoringPolicy.type"));
    }

    @Test
    @DisplayName("invalid scoringPolicy.rollup slug rejected")
    void badRollup() {
        String yaml = VALID_YAML.replace("rollup: worst-of", "rollup: average");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("scoringPolicy.rollup"));
    }

    @Test
    @DisplayName("missing scoringPolicy rejected (required)")
    void missingScoringPolicy() {
        String yaml = VALID_YAML.replace(
                "scoringPolicy:\n  type: piqi-dimension-scorecard\n  rollup: worst-of\n", "");
        assertThat(beanViolations(yaml)).anyMatch(v -> v.startsWith("scoringPolicy:"));
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("scoringPolicy: is required"));
    }

    @Test
    @DisplayName("missing scoringPolicy.rollup rejected (required when scoringPolicy present)")
    void missingRollup() {
        String yaml = VALID_YAML.replace("  rollup: worst-of\n", "");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("scoringPolicy.rollup: is required"));
    }

    @Test
    @DisplayName("missing severityOverride rejected (required on every check)")
    void missingSeverityOverride() {
        String yaml = VALID_YAML.replace("    severityOverride: ERROR\n", "");
        assertThat(beanViolations(yaml)).anyMatch(v -> v.contains("severityOverride:"));
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("severityOverride is required"));
    }

    @Test
    @DisplayName("unknown scoringPolicy key rejected")
    void unknownScoringPolicyKey() {
        String yaml = VALID_YAML.replace("  rollup: worst-of", "  rollup: worst-of\n  weights: {}");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("scoringPolicy: unknown property 'weights'"));
    }

    @Test
    @DisplayName("invalid FHIR resource type in applicableContext rejected")
    void badFhirResource() {
        String yaml = VALID_YAML.replace("- Bundle", "- Patiant");
        assertThat(domainErrors(yaml))
                .anyMatch(e -> e.contains("'Patiant' is not a valid FHIR R4 resource type"));
    }

    @Test
    @DisplayName("unknown applicableContext key rejected")
    void unknownContextKey() {
        String yaml = VALID_YAML.replace("  workflowTags:", "  facilities:\n    - f1\n  workflowTags:");
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("applicableContext: unknown property 'facilities'"));
    }

    @Test
    @DisplayName("workflow tag over 128 chars rejected")
    void workflowTagTooLong() {
        String yaml = VALID_YAML.replace("- submission", "- " + "w".repeat(129));
        assertThat(domainErrors(yaml)).anyMatch(e -> e.contains("must be at most 128 characters"));
    }

    @Test
    @DisplayName("one broken YAML accumulates errors from multiple layers' rules at once")
    void multipleDomainErrorsAccumulate() {
        String yaml = VALID_YAML
                .replace("expression: Patient.name.exists()", "expression: \"Patient.name.exists(\"")
                .replace("type: piqi-dimension-scorecard", "type: bogus")
                .replace("- Bundle", "- Patiant");
        List<String> errors = domainErrors(yaml);
        assertThat(errors)
                .anyMatch(e -> e.contains("invalid FHIRPath expression"))
                .anyMatch(e -> e.contains("scoringPolicy.type"))
                .anyMatch(e -> e.contains("not a valid FHIR R4 resource type"));
    }
}
