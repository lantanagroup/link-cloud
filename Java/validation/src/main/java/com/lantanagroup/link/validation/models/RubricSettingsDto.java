package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import lombok.Builder;
import lombok.Getter;

import java.util.List;
import java.util.Map;

/**
 * What a client needs to know about how this service treats rubrics before it sends one: the limits,
 * the values it accepts, and the settings that change what an action does.
 */
@Getter
@Builder
public class RubricSettingsDto {
    // whether $publish is refused until a dry run has completed for the version's current definition
    private boolean dryRunRequiredForPublish;
    // the rubric the reporting pipeline evaluates against (vaas.bridge.rubric-id)
    private String pipelineRubricId;
    // whether the pipeline decides on the rubric engine's result (vaas.bridge.enabled)
    private boolean pipelineEnabled;
    // whether the pipeline also runs the rubric engine alongside the engine that decides (vaas.bridge.shadow-enabled)
    private boolean pipelineShadowEnabled;

    private int maxChecks;
    private int maxPayloadBytes;
    private int maxProfiles;
    private int maxCombinatorChecks;
    private int maxCombinatorDepth;
    private int maxApplicableContextEntries;

    private List<CheckTypeSetting> checkTypes;
    private List<PiqiDimension> dimensions;
    private List<Severity> severities;
    private List<ScoringPolicySetting> scoringPolicies;
    private List<String> rollups;
    private List<String> matchModes;
    // the FHIR resource types applicableContext.fhirResources accepts
    private List<String> fhirResourceTypes;

    /**
     * @param type            the check type
     * @param dimension       the dimension a check of this type must use; null when it may use any the rubric declares
     * @param parameters      the parameter keys a check of this type accepts
     * @param supported       false for a type the engine defines but cannot run yet
     */
    public record CheckTypeSetting(CheckType type, PiqiDimension dimension, List<String> parameters, boolean supported) {
    }

    /**
     * @param type           the slug a definition gives as scoringPolicy.type
     * @param rollupRequired true when scoringPolicy.rollup must be given; false when it must be absent
     */
    public record ScoringPolicySetting(String type, boolean rollupRequired) {
    }
}
