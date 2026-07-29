package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.services.execution.executors.CustomCheckExecutor;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

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

    private CustomCheckExecutor customCheckExecutor;
    private RubricDefinitionValidator validator;

    @BeforeEach
    void setUp() {
        customCheckExecutor = mock(CustomCheckExecutor.class);
        validator = new RubricDefinitionValidator(customCheckExecutor);
    }

    private static JsonNode params(String json) {
        try {
            return JSON.readTree(json);
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    private static CheckDto.CheckDtoBuilder check(String id, CheckType type, PiqiDimension dim) {
        return CheckDto.builder().id(id).type(type).dimension(dim).ordinal(0).enabled(true);
    }

    private static RubricVersionPayloadDto.RubricVersionPayloadDtoBuilder payload(CheckDto... checks) {
        return RubricVersionPayloadDto.builder()
                .id("piqi.core")
                .semver("1.0.0")
                .checks(List.of(checks));
    }

    // ---- Happy paths -----------------------------------------------------------------------------

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
                check("c2", CheckType.TERMINOLOGY, PiqiDimension.TERMINOLOGY).build()
        ).dimensions(List.of(PiqiDimension.CONFORMANCE, PiqiDimension.TERMINOLOGY)).build();
        assertThatCode(() -> validator.validate(p)).doesNotThrowAnyException();
    }

    // ---- Structural failures ---------------------------------------------------------------------

    @Test
    @DisplayName("empty checks -> rejected")
    void emptyChecks() {
        var p = RubricVersionPayloadDto.builder().id("piqi.core").semver("1.0.0").checks(List.of()).build();
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
                .isInstanceOf(InvalidRubricDefinitionException.class)
                .hasMessageContaining("Invalid rubric definition");
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
        var p = payload(
                check("dup", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"x\"}")).build(),
                check("dup", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE)
                        .parameters(params("{\"expression\":\"y\"}")).build()
        ).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("missing dimension -> rejected")
    void missingDimension() {
        var p = payload(CheckDto.builder().id("c1").type(CheckType.FHIRPATH).ordinal(0).enabled(true)
                .parameters(params("{\"expression\":\"x\"}")).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("check dimension not declared in rubric dimensions -> rejected")
    void undeclaredDimension() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CURRENCY)
                .parameters(params("{\"expression\":\"x\"}")).build())
                .dimensions(List.of(PiqiDimension.CONFORMANCE)).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    // ---- Per-type parameter failures -------------------------------------------------------------

    @Test
    @DisplayName("FHIRPATH without expression -> rejected")
    void fhirpathMissingExpression() {
        var p = payload(check("c1", CheckType.FHIRPATH, PiqiDimension.CONFORMANCE).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
    }

    @Test
    @DisplayName("VALUESET without path/valueSet -> rejected")
    void valuesetMissingParams() {
        var p = payload(check("c1", CheckType.VALUESET, PiqiDimension.TERMINOLOGY).build()).build();
        assertThatThrownBy(() -> validator.validate(p))
                .isInstanceOf(InvalidRubricDefinitionException.class);
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
}
