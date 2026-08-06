package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.services.execution.executors.CustomCheckExecutor;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Unit coverage for the real {@link RubricDefinitionValidator} logic across a matrix of check-type
 * and data variations. These assert the intended semantic-validation behaviour directly (no HTTP
 * layer), so they document what the validator accepts and rejects.
 */
class RubricDefinitionValidatorTest {

    private static final ObjectMapper JSON = new ObjectMapper();
    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final IFhirPath FHIR_PATH = FHIR_CONTEXT.newFhirPath();
    private static final int MAX_CHECKS = 200;

    private CustomCheckExecutor customCheckExecutor;
    private RubricDefinitionValidator validator;

    @BeforeEach
    void setUp() {
        customCheckExecutor = mock(CustomCheckExecutor.class);
        validator = new RubricDefinitionValidator(customCheckExecutor, FHIR_PATH,
                new ScoringPolicyValidator(), new ApplicableContextValidator(FHIR_CONTEXT), MAX_CHECKS);
    }

    private static JsonNode params(String json) {
        try {
            return JSON.readTree(json);
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    private static CheckDto.CheckDtoBuilder check(String id, CheckType type, PiqiDimension dim) {
        return CheckDto.builder().id(id).type(type).dimension(dim)
                .severityOverride(Severity.ERROR).ordinal(0).enabled(true);
    }

    private static RubricVersionPayloadDto.RubricVersionPayloadDtoBuilder payload(CheckDto... checks) {
        return RubricVersionPayloadDto.builder()
                .id("piqi.core")
                .semver("1.0.0")
                .dimensions(List.of(PiqiDimension.values()))
                .scoringPolicy(params("{\"type\":\"piqi-dimension-scorecard\",\"rollup\":\"worst-of\"}"))
                .checks(List.of(checks));
    }

    private static List<String> errorsOf(ThrowingCall call) {
        try {
            call.run();
        } catch (InvalidRubricDefinitionException e) {
            return e.getErrors();
        }
        throw new AssertionError("Expected InvalidRubricDefinitionException but nothing was thrown");
    }

    private interface ThrowingCall {
        void run();
    }

    //  Happy paths

    @Test
    @DisplayName("valid FHIRPATH check passes")
    void validFhirpath() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                .parameters(params("{\"expression\":\"Patient.name.exists()\"}")).build()).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("valid VALUESET check passes")
    void validValueset() {
        var p = payload(check("c1", CheckType.VALUESET, PiqiDimension.TERMINOLOGY)
                .parameters(params("{\"path\":\"Observation.code\",\"valueSet\":\"http://x/vs\"}")).build()).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("valid CUSTOM check (resolvable plug-in) passes")
    void validCustom() {
        when(customCheckExecutor.canResolve(any(), any())).thenReturn(true);
        var p = payload(check("c1", CheckType.CUSTOM, PiqiDimension.PLAUSIBILITY)
                .parameters(params("{\"customCheckId\":\"my-check\"}")).build()).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("FHIR_CONFORMANCE / TERMINOLOGY need no parameters")
    void parameterlessTypes() {
        var p = payload(
                check("c1", CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE).build(),
                check("c2", CheckType.TERMINOLOGY, PiqiDimension.TERMINOLOGY).ordinal(1).build()
        ).dimensions(List.of(PiqiDimension.CONFORMANCE, PiqiDimension.TERMINOLOGY)).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("valid scoringPolicy and applicableContext pass")
    void validPolicyAndContext() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                .parameters(params("{\"expression\":\"Patient.name.exists()\"}")).build())
                .scoringPolicy(params("{\"type\":\"piqi-dimension-scorecard\",\"rollup\":\"worst-of\"}"))
                .applicableContext(params("{\"fhirResources\":[\"Bundle\",\"Patient\"],\"workflowTags\":[\"submission\"]}"))
                .build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    //  Structural failures 

    @Test
    @DisplayName("empty checks -> rejected")
    void emptyChecks() {
        var p = RubricVersionPayloadDto.builder().id("piqi.core").semver("1.0.0")
                .dimensions(List.of(PiqiDimension.CONFORMANCE)).checks(List.of()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class)
                .hasMessageContaining("Invalid rubric definition");
    }

    @Test
    @DisplayName("blank id -> rejected")
    void blankId() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                .parameters(params("{\"expression\":\"x\"}")).build()).id("  ").build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("invalid semver -> rejected")
    void badSemver() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                .parameters(params("{\"expression\":\"x\"}")).build()).semver("v1").build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("duplicate check ids -> rejected")
    void duplicateCheckIds() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("dup", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"x\"}")).build(),
                check("dup", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(1)
                        .parameters(params("{\"expression\":\"y\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("duplicate check id"));
    }

    @Test
    @DisplayName("missing dimension -> rejected")
    void missingDimension() {
        var p = payload(CheckDto.builder().id("c1").type(CheckType.FHIRPATH)
                .severityOverride(Severity.ERROR).ordinal(0).enabled(true)
                .parameters(params("{\"expression\":\"x\"}")).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("check dimension not declared in rubric dimensions -> rejected")
    void undeclaredDimension() {
        var errors = errorsOf(() -> validator.validate(
                payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CURRENCY)
                        .parameters(params("{\"expression\":\"x\"}")).build())
                        .dimensions(List.of(PiqiDimension.CONFORMANCE)).build()));
        assertThat(errors).anyMatch(e -> e.contains("not declared in the rubric's dimensions"));
    }

    @Test
    @DisplayName("missing dimensions list -> rejected")
    void missingDimensions() {
        var errors = errorsOf(() -> validator.validate(
                payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"x\"}")).build())
                        .dimensions(null).build()));
        assertThat(errors).anyMatch(e -> e.contains("dimensions: at least one dimension is required"));
    }

    @Test
    @DisplayName("null entry / duplicate in dimensions -> rejected")
    void badDimensionEntries() {
        var errors = errorsOf(() -> validator.validate(
                payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"x\"}")).build())
                        .dimensions(Arrays.asList(PiqiDimension.CONFORMANCE, null, PiqiDimension.CONFORMANCE))
                        .build()));
        assertThat(errors)
                .anyMatch(e -> e.contains("must not contain null entries"))
                .anyMatch(e -> e.contains("duplicate dimension CONFORMANCE"));
    }

    @Test
    @DisplayName("missing severityOverride -> rejected")
    void missingSeverityOverride() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).severityOverride(null)
                        .parameters(params("{\"expression\":\"x\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("severityOverride is required"));
    }

    @Test
    @DisplayName("duplicate ordinal -> rejected")
    void duplicateOrdinal() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"x\"}")).build(),
                check("c2", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"y\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("duplicate ordinal 0"));
    }

    @Test
    @DisplayName("multiple checks without an ordinal are allowed")
    void nullOrdinalsAllowed() {
        var p = payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(null)
                        .parameters(params("{\"expression\":\"x\"}")).build(),
                check("c2", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(null)
                        .parameters(params("{\"expression\":\"y\"}")).build()
        ).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("mixing checks with and without ordinals is allowed")
    void mixedNullAndExplicitOrdinals() {
        var p = payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(null)
                        .parameters(params("{\"expression\":\"x\"}")).build(),
                check("c2", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(0)
                        .parameters(params("{\"expression\":\"y\"}")).build(),
                check("c3", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(1)
                        .parameters(params("{\"expression\":\"z\"}")).build()
        ).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("duplicate explicit ordinal is still rejected when null ordinals are present")
    void duplicateExplicitOrdinalAmongNulls() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(null)
                        .parameters(params("{\"expression\":\"x\"}")).build(),
                check("c2", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(1)
                        .parameters(params("{\"expression\":\"y\"}")).build(),
                check("c3", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(1)
                        .parameters(params("{\"expression\":\"z\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("duplicate ordinal 1"));
        assertThat(errors).noneMatch(e -> e.contains("checks[c1]"));
    }

    @Test
    @DisplayName("negative ordinal -> rejected by the domain validator")
    void negativeOrdinalRejected() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(-1)
                        .parameters(params("{\"expression\":\"x\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("checks[c1]: ordinal must be >= 0"));
    }

    @Test
    @DisplayName("two checks with the same type, dimension, and no parameters are duplicates")
    void duplicateDefinitionWithNullParameters() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE).ordinal(0).build(),
                check("c2", CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE).ordinal(1).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("checks[c2]: duplicate of check 'c1'"));
    }

    @Test
    @DisplayName("two checks with the same type, dimension, and parameters -> rejected")
    void duplicateDefinition() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(0)
                        .parameters(params("{\"expression\":\"Patient.name.exists()\"}")).build(),
                check("c2", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(1)
                        .parameters(params("{\"expression\":\"Patient.name.exists()\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("checks[c2]: duplicate of check 'c1'"));
    }

    @Test
    @DisplayName("duplicate detection is structural — parameter key order does not evade it")
    void duplicateDefinitionIgnoresKeyOrder() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(0)
                        .parameters(params("{\"expression\":\"x\",\"code\":\"k\"}")).build(),
                check("c2", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(1)
                        .parameters(params("{\"code\":\"k\",\"expression\":\"x\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("checks[c2]: duplicate of check 'c1'"));
    }

    @Test
    @DisplayName("same parameters under a different type or dimension are not duplicates")
    void sameParametersDifferentTypeAllowed() {
        var p = payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).ordinal(0)
                        .parameters(params("{\"expression\":\"Patient.name.exists()\"}")).build(),
                check("c2", CheckType.COMPLETENESS, PiqiDimension.COMPLETENESS).ordinal(1)
                        .parameters(params("{\"expression\":\"Patient.name.exists()\"}")).build()
        ).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("more than max checks -> rejected; exactly max passes")
    void maxChecksCap() {
        var small = new RubricDefinitionValidator(customCheckExecutor, FHIR_PATH,
                new ScoringPolicyValidator(), new ApplicableContextValidator(FHIR_CONTEXT), 2);
        List<CheckDto> three = new ArrayList<>();
        for (int i = 0; i < 3; i++) {
            three.add(check("c" + i, CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                    .ordinal(i).parameters(params("{\"expression\":\"x" + i + "\"}")).build());
        }
        var over = payload().checks(three).build();
        var errors = errorsOf(() -> small.validate(over));
        assertThat(errors).anyMatch(e -> e.contains("at most 2 checks are allowed (got 3)"));

        var atCap = payload().checks(three.subList(0, 2)).build();
        assertThatCode(() -> small.validate(atCap)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("all checks disabled -> rejected")
    void allChecksDisabled() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).enabled(false)
                        .parameters(params("{\"expression\":\"x\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("at least one check must be enabled"));
    }

    // Type/dimension coherence
    @Test
    @DisplayName("VALUESET with non-TERMINOLOGY dimension -> rejected")
    void valuesetWrongDimension() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.VALUESET, PiqiDimension.COMPLETENESS)
                        .parameters(params("{\"path\":\"Observation.code\",\"valueSet\":\"http://x/vs\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("type VALUESET must use dimension TERMINOLOGY, got COMPLETENESS"));
    }

    @ParameterizedTest
    @EnumSource(value = CheckType.class, names = {"COMPLETENESS", "PLAUSIBILITY", "CURRENCY", "TERMINOLOGY"})
    @DisplayName("self-named types must use their namesake dimension")
    void selfNamedTypesRequireNamesakeDimension(CheckType type) {
        PiqiDimension wrong = type.name().equals("COMPLETENESS")
                ? PiqiDimension.CURRENCY : PiqiDimension.COMPLETENESS;
        var builder = check("c1", type, wrong);
        if (type != CheckType.TERMINOLOGY) {
            builder.parameters(params("{\"expression\":\"x\"}"));
        }
        var errors = errorsOf(() -> validator.validate(payload(builder.build()).build()));
        assertThat(errors).anyMatch(e -> e.contains("type " + type + " must use dimension"));
    }

    @Test
    @DisplayName("FHIRPATH may use any declared dimension")
    void fhirpathAnyDimension() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CURRENCY)
                .parameters(params("{\"expression\":\"Patient.birthDate.exists()\"}")).build()).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    //  Per-type parameter failures 

    @Test
    @DisplayName("FHIRPATH without expression -> rejected")
    void fhirpathMissingExpression() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("invalid FHIRPath expression -> rejected")
    void invalidFhirPathExpression() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"Patient.name.exists(\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("invalid FHIRPath expression"));
    }

    @Test
    @DisplayName("unknown parameter key (typo) -> rejected")
    void unknownParameterKey() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expresion\":\"Patient.name.exists()\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("unknown property 'expresion'"));
    }

    @Test
    @DisplayName("oversized finding code parameter -> rejected")
    void codeTooLong() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"x\",\"code\":\"" + "c".repeat(129) + "\"}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("parameters.code: must be at most 128 characters"));
    }

    @Test
    @DisplayName("TERMINOLOGY: bad whitelist regex / non-boolean validateCodings -> rejected")
    void terminologyBadParameters() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.TERMINOLOGY, PiqiDimension.TERMINOLOGY)
                        .parameters(params("{\"validateCodings\":\"yes\",\"valueSetWhitelistRegex\":\"[unclosed\"}")).build()
        ).build()));
        assertThat(errors)
                .anyMatch(e -> e.contains("validateCodings: must be a boolean"))
                .anyMatch(e -> e.contains("invalid regular expression"));
    }

    @Test
    @DisplayName("VALUESET without path/valueSet -> rejected")
    void valuesetMissingParams() {
        var p = payload(check("c1", CheckType.VALUESET, PiqiDimension.TERMINOLOGY).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("VALUESET: invalid path FHIRPath and non-URI valueSet -> rejected")
    void valuesetBadParams() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.VALUESET, PiqiDimension.TERMINOLOGY)
                        .parameters(params("{\"path\":\"Observation..code(\",\"valueSet\":\"not a url\"}")).build()
        ).build()));
        assertThat(errors)
                .anyMatch(e -> e.contains("parameters.path: invalid FHIRPath expression"))
                .anyMatch(e -> e.contains("parameters.valueSet: must be a canonical URL"));
    }

    @Test
    @DisplayName("VALUESET: versioned canonical URL (|version) accepted")
    void valuesetVersionedCanonical() {
        var p = payload(check("c1", CheckType.VALUESET, PiqiDimension.TERMINOLOGY)
                .parameters(params("{\"path\":\"Observation.code\",\"valueSet\":\"http://hl7.org/fhir/ValueSet/observation-codes|4.0.1\"}"))
                .build()).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("canonical URL edge cases: versioned accepted, double-pipe and blank version rejected")
    void canonicalUrlEdgeCases() {
        var ok = payload(check("c1", CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE)
                .parameters(params("{\"profiles\":[\"http://x/StructureDefinition/y|2.1.0\"]}")).build()).build();
        assertThatCode(() -> validator.validate(ok)).doesNotThrowAnyException();

        var doublePipe = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"profiles\":[\"http://x/sd|1|2\"]}")).build()).build()));
        assertThat(doublePipe).anyMatch(e -> e.contains("must be a canonical URL"));

        var blankVersion = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"profiles\":[\"http://x/sd|\"]}")).build()).build()));
        assertThat(blankVersion).anyMatch(e -> e.contains("must be a canonical URL"));
    }

    @Test
    @DisplayName("TERMINOLOGY with valid parameters passes")
    void terminologyValidParameters() {
        var p = payload(check("c1", CheckType.TERMINOLOGY, PiqiDimension.TERMINOLOGY)
                .parameters(params("{\"validateCodings\":true,\"valueSetWhitelistRegex\":\"^http://hl7\\\\.org/.*$\"}"))
                .build()).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("CUSTOM with valid min/max passes")
    void customValidMinMax() {
        when(customCheckExecutor.canResolve(any(), any())).thenReturn(true);
        var p = payload(check("c1", CheckType.CUSTOM, PiqiDimension.PLAUSIBILITY)
                .parameters(params("{\"customCheckId\":\"numeric-range\",\"path\":\"Observation.value\",\"min\":0,\"max\":300}"))
                .build()).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("FHIR_CONFORMANCE: profiles must be unique canonical URLs, max 20")
    void conformanceBadProfiles() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIR_CONFORMANCE, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"profiles\":[\"not a url\",\"http://x/sd\",\"http://x/sd\"]}")).build()
        ).build()));
        assertThat(errors)
                .anyMatch(e -> e.contains("profiles[0]: must be a canonical URL"))
                .anyMatch(e -> e.contains("duplicate profile 'http://x/sd'"));
    }

    @Test
    @DisplayName("CUSTOM without customCheckId/className -> rejected")
    void customMissingIdentifier() {
        var p = payload(check("c1", CheckType.CUSTOM, PiqiDimension.PLAUSIBILITY).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("CUSTOM with unresolvable plug-in -> rejected")
    void customUnresolvable() {
        when(customCheckExecutor.canResolve(any(), any())).thenReturn(false);
        var p = payload(check("c1", CheckType.CUSTOM, PiqiDimension.PLAUSIBILITY)
                .parameters(params("{\"customCheckId\":\"ghost\"}")).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("CUSTOM: min > max and non-numeric min -> rejected")
    void customBadMinMax() {
        when(customCheckExecutor.canResolve(any(), any())).thenReturn(true);
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.CUSTOM, PiqiDimension.PLAUSIBILITY)
                        .parameters(params("{\"customCheckId\":\"numeric-range\",\"min\":10,\"max\":5}")).build()
        ).build()));
        assertThat(errors).anyMatch(e -> e.contains("min (10.0) must be <= max (5.0)"));

        var errors2 = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.CUSTOM, PiqiDimension.PLAUSIBILITY)
                        .parameters(params("{\"customCheckId\":\"numeric-range\",\"min\":\"low\"}")).build()
        ).build()));
        assertThat(errors2).anyMatch(e -> e.contains("parameters.min: must be a number"));
    }


    @Test
    @DisplayName("missing scoringPolicy -> rejected")
    void missingScoringPolicy() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"x\"}")).build()
        ).scoringPolicy(null).build()));
        assertThat(errors).anyMatch(e -> e.contains("scoringPolicy: is required"));
    }

    @Test
    @DisplayName("bad scoringPolicy and bad check accumulate in one exception")
    void multiErrorAccumulation() {
        var errors = errorsOf(() -> validator.validate(payload(
                check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"Patient.name.exists(\"}")).build()
        ).scoringPolicy(params("{\"type\":\"bogus\"}")).build()));
        assertThat(errors)
                .anyMatch(e -> e.contains("invalid FHIRPath expression"))
                .anyMatch(e -> e.contains("scoringPolicy.type"));
    }
}
