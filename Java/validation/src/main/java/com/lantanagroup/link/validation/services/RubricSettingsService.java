package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.configs.RubricDryRunConfig;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RubricSettingsDto;
import com.lantanagroup.link.validation.services.execution.executors.CustomCheckExecutor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.util.Arrays;
import java.util.List;

/**
 * Describes how this service treats rubrics, for a client that builds a definition: the limits and the
 * values the validators enforce, read from the validators themselves so the two cannot drift apart, and
 * the settings that change what publishing and retiring do.
 */
@Service
public class RubricSettingsService {

    private final RubricDefinitionValidator definitionValidator;
    private final ApplicableContextValidator applicableContextValidator;
    private final CustomCheckExecutor customCheckExecutor;
    private final RubricDryRunConfig dryRunConfig;
    private final int maxPayloadBytes;
    private final String pipelineRubricId;
    private final boolean pipelineEnabled;
    private final boolean pipelineShadowEnabled;

    public RubricSettingsService(RubricDefinitionValidator definitionValidator,
                                 ApplicableContextValidator applicableContextValidator,
                                 CustomCheckExecutor customCheckExecutor,
                                 RubricDryRunConfig dryRunConfig,
                                 @Value("${link.rubric.max-payload-bytes:262144}") int maxPayloadBytes,
                                 @Value("${vaas.bridge.rubric-id:measure-report-submission-v1}") String pipelineRubricId,
                                 @Value("${vaas.bridge.enabled:false}") boolean pipelineEnabled,
                                 @Value("${vaas.bridge.shadow-enabled:false}") boolean pipelineShadowEnabled) {
        this.definitionValidator = definitionValidator;
        this.applicableContextValidator = applicableContextValidator;
        this.customCheckExecutor = customCheckExecutor;
        this.dryRunConfig = dryRunConfig;
        this.maxPayloadBytes = maxPayloadBytes;
        this.pipelineRubricId = pipelineRubricId;
        this.pipelineEnabled = pipelineEnabled;
        this.pipelineShadowEnabled = pipelineShadowEnabled;
    }

    public RubricSettingsDto getSettings() {
        return RubricSettingsDto.builder()
                .dryRunRequiredForPublish(dryRunConfig.isRequiredForPublish())
                .pipelineRubricId(pipelineRubricId)
                .pipelineEnabled(pipelineEnabled)
                .pipelineShadowEnabled(pipelineShadowEnabled)
                .maxChecks(definitionValidator.getMaxChecks())
                .maxPayloadBytes(maxPayloadBytes)
                .maxProfiles(RubricDefinitionValidator.MAX_PROFILES)
                .maxCombinatorChecks(RubricDefinitionValidator.MAX_COMBINATOR_CHECKS)
                .maxCombinatorDepth(RubricDefinitionValidator.MAX_COMBINATOR_DEPTH)
                .maxApplicableContextEntries(ApplicableContextValidator.MAX_ENTRIES)
                .checkTypes(Arrays.stream(CheckType.values()).map(this::describe).toList())
                .dimensions(List.of(PiqiDimension.values()))
                .severities(List.of(Severity.values()))
                .scoringPolicies(Arrays.stream(ScoringPolicyType.values())
                        .map(type -> new RubricSettingsDto.ScoringPolicySetting(
                                type.getValue(), type != ScoringPolicyType.PIQI_PASS_FAIL))
                        .toList())
                .rollups(RollupStrategy.allowedValues())
                .matchModes(RubricDefinitionValidator.COMBINATOR_MATCH_MODES.stream().sorted().toList())
                .fhirResourceTypes(applicableContextValidator.getFhirResourceTypes().stream().sorted().toList())
                .build();
    }

    private RubricSettingsDto.CheckTypeSetting describe(CheckType type) {
        // a CUSTOM check names a loaded plug-in; with none loaded there is nothing it could name
        boolean supported = type != CheckType.CUSTOM || customCheckExecutor.hasPlugins();
        return new RubricSettingsDto.CheckTypeSetting(
                type,
                RubricDefinitionValidator.requiredDimension(type),
                RubricDefinitionValidator.allowedParameterKeys(type).stream().sorted().toList(),
                supported);
    }
}
