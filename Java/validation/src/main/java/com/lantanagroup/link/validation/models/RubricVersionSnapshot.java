package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.enums.Severity;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

/**
 * Cached form of a rubric version + its live checks, so one cache hit covers everything the
 * evaluate path reads. The entities themselves can't be cached: their lazy associations
 * don't survive serialization. Only columns the evaluate path never reads are left out
 * (definition/dimensions LOBs, dry-run and audit fields) — scoringPolicyJson IS carried because
 * the assembler scores with it; dropping it would silently fall back to the default
 * dimension/worst-of policy (see ResultEnvelopeAssembler). No dry-run fields means recordDryRun
 * doesn't have to evict.
 *
 * Has to stay a plain non-final class (same for CheckSnapshot) — the redis serializer uses
 * NON_FINAL default typing, so a record would come back as a LinkedHashMap.
 */
@Data
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RubricVersionSnapshot {

    private Long rubricVersionId;
    private String rubricId;
    private String semver;
    private RubricVersionStatus status;
    private String checksum;
    private String scoringPolicyJson;
    private List<CheckSnapshot> checks;

    @Data
    @NoArgsConstructor
    @AllArgsConstructor
    @Builder
    public static class CheckSnapshot {
        private Long checkId;
        private Long rubricVersionId;
        private String checkLocalId;
        private CheckType type;
        private PiqiDimension dimension;
        private String parametersJson;
        private Severity severityOverride;
        private Integer ordinal;
        private boolean enabled;
    }

    public static RubricVersionSnapshot from(RubricVersion version, List<RubricCheck> checks) {
        return RubricVersionSnapshot.builder()
                .rubricVersionId(version.getRubricVersionId())
                .rubricId(version.getRubricId())
                .semver(version.getSemver())
                .status(version.getStatus())
                .checksum(version.getChecksum())
                .scoringPolicyJson(version.getScoringPolicyJson())
                .checks(checks.stream()
                        .map(c -> CheckSnapshot.builder()
                                .checkId(c.getCheckId())
                                .rubricVersionId(c.getRubricVersionId())
                                .checkLocalId(c.getCheckLocalId())
                                .type(c.getType())
                                .dimension(c.getDimension())
                                .parametersJson(c.getParametersJson())
                                .severityOverride(c.getSeverityOverride())
                                .ordinal(c.getOrdinal())
                                .enabled(c.isEnabled())
                                .build())
                        .toList())
                .build();
    }

    // detached and only partially populated — never save() this
    public RubricVersion toVersionEntity() {
        return RubricVersion.builder()
                .rubricVersionId(rubricVersionId)
                .rubricId(rubricId)
                .semver(semver)
                .status(status)
                .checksum(checksum)
                .scoringPolicyJson(scoringPolicyJson)
                .build();
    }

    // detached and only partially populated — never save() these
    public List<RubricCheck> toCheckEntities() {
        return checks.stream()
                .map(c -> RubricCheck.builder()
                        .checkId(c.getCheckId())
                        .rubricVersionId(c.getRubricVersionId())
                        .checkLocalId(c.getCheckLocalId())
                        .type(c.getType())
                        .dimension(c.getDimension())
                        .parametersJson(c.getParametersJson())
                        .severityOverride(c.getSeverityOverride())
                        .ordinal(c.getOrdinal())
                        .enabled(c.isEnabled())
                        .build())
                .toList();
    }
}
