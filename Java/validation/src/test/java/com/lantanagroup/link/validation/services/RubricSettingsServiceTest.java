package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.configs.RubricDryRunConfig;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RubricSettingsDto;
import com.lantanagroup.link.validation.models.RubricSettingsDto.CheckTypeSetting;
import com.lantanagroup.link.validation.models.RubricSettingsDto.ScoringPolicySetting;
import com.lantanagroup.link.validation.services.execution.executors.CustomCheckExecutor;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class RubricSettingsServiceTest {

    private final RubricDefinitionValidator definitionValidator = mock(RubricDefinitionValidator.class);
    private final CustomCheckExecutor customCheckExecutor = mock(CustomCheckExecutor.class);
    private final RubricDryRunConfig dryRunConfig = new RubricDryRunConfig();

    private RubricSettingsDto settings() {
        when(definitionValidator.getMaxChecks()).thenReturn(150);
        return new RubricSettingsService(definitionValidator, new ApplicableContextValidator(FhirContext.forR4Cached()),
                customCheckExecutor, dryRunConfig, 4096, "bare-minimum-requirements", true, false).getSettings();
    }

    private static CheckTypeSetting type(RubricSettingsDto settings, CheckType type) {
        return settings.getCheckTypes().stream().filter(t -> t.type() == type).findFirst().orElseThrow();
    }

    @Test
    @DisplayName("carries the configured limits and pipeline settings")
    void configuredValues() {
        dryRunConfig.setRequiredForPublish(true);

        RubricSettingsDto settings = settings();

        assertThat(settings.isDryRunRequiredForPublish()).isTrue();
        assertThat(settings.getPipelineRubricId()).isEqualTo("bare-minimum-requirements");
        assertThat(settings.isPipelineEnabled()).isTrue();
        assertThat(settings.isPipelineShadowEnabled()).isFalse();
        assertThat(settings.getMaxChecks()).isEqualTo(150);
        assertThat(settings.getMaxPayloadBytes()).isEqualTo(4096);
    }

    @Test
    @DisplayName("the limits are the validators' own")
    void validatorLimits() {
        RubricSettingsDto settings = settings();

        assertThat(settings.getMaxProfiles()).isEqualTo(RubricDefinitionValidator.MAX_PROFILES);
        assertThat(settings.getMaxCombinatorChecks()).isEqualTo(RubricDefinitionValidator.MAX_COMBINATOR_CHECKS);
        assertThat(settings.getMaxCombinatorDepth()).isEqualTo(RubricDefinitionValidator.MAX_COMBINATOR_DEPTH);
        assertThat(settings.getMaxApplicableContextEntries()).isEqualTo(ApplicableContextValidator.MAX_ENTRIES);
        assertThat(settings.getMatchModes()).containsExactly("ALL", "ANY");
    }

    @Test
    @DisplayName("every check type is described, with the dimension it is pinned to and the parameters it accepts")
    void checkTypes() {
        RubricSettingsDto settings = settings();

        assertThat(settings.getCheckTypes()).extracting(CheckTypeSetting::type).containsExactly(CheckType.values());
        assertThat(type(settings, CheckType.VALUESET).dimension()).isEqualTo(PiqiDimension.TERMINOLOGY);
        assertThat(type(settings, CheckType.VALUESET).parameters())
                .containsExactly("checks", "code", "failureMessage", "matchMode", "path", "system", "valueSet");
        assertThat(type(settings, CheckType.COMPLETENESS).dimension()).isEqualTo(PiqiDimension.COMPLETENESS);
        // FHIRPATH may use any dimension the rubric declares
        assertThat(type(settings, CheckType.FHIRPATH).dimension()).isNull();
        assertThat(type(settings, CheckType.FHIRPATH).parameters()).containsExactly("code", "expression", "failureMessage");
        assertThat(type(settings, CheckType.FHIR_CONFORMANCE).parameters()).containsExactly("profiles");
    }

    @Test
    @DisplayName("CUSTOM is unsupported with no plug-in loaded, and supported with one; every other type is supported")
    void customSupport() {
        when(customCheckExecutor.hasPlugins()).thenReturn(false);
        RubricSettingsDto without = settings();
        assertThat(type(without, CheckType.CUSTOM).supported()).isFalse();
        assertThat(without.getCheckTypes()).filteredOn(t -> t.type() != CheckType.CUSTOM).allMatch(CheckTypeSetting::supported);

        when(customCheckExecutor.hasPlugins()).thenReturn(true);
        assertThat(type(settings(), CheckType.CUSTOM).supported()).isTrue();
    }

    @Test
    @DisplayName("scoring policies say whether a roll-up is required: not for pass/fail, required for the scorecards")
    void scoringPolicies() {
        RubricSettingsDto settings = settings();

        assertThat(settings.getScoringPolicies()).containsExactly(
                new ScoringPolicySetting("piqi-dimension-scorecard", true),
                new ScoringPolicySetting("piqi-check-scorecard", true),
                new ScoringPolicySetting("piqi-pass-fail", false));
        assertThat(settings.getRollups()).containsExactly("worst-of", "best-of", "pass-fail", "majority", "all-must-pass");
        assertThat(settings.getDimensions()).containsExactly(PiqiDimension.values());
        assertThat(settings.getSeverities()).containsExactly(Severity.values());
    }

    @Test
    @DisplayName("the FHIR resource types are the R4 ones the applicable context accepts, in order")
    void fhirResourceTypes() {
        List<String> types = settings().getFhirResourceTypes();

        assertThat(types).contains("Patient", "Observation", "DiagnosticReport").isSorted().doesNotHaveDuplicates();
        assertThat(types).doesNotContain("NotAResource");
    }
}
