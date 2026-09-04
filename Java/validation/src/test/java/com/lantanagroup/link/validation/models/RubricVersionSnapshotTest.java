package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.enums.Severity;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class RubricVersionSnapshotTest {

    private static final String POLICY_JSON = "{\"type\":\"piqi-check-scorecard\",\"rollup\":\"worst-of\"}";

    private final Long versionId = 1L;
    private final Long checkId = 100L;

    private RubricVersion version() {
        return RubricVersion.builder()
                .rubricVersionId(versionId)
                .rubricId("piqi.core")
                .semver("1.2.0")
                .status(RubricVersionStatus.PUBLISHED)
                .checksum("abc123")
                .scoringPolicyJson(POLICY_JSON)
                .build();
    }

    private RubricCheck check() {
        return RubricCheck.builder()
                .checkId(checkId)
                .rubricVersionId(versionId)
                .checkLocalId("c1")
                .type(CheckType.FHIRPATH)
                .dimension(PiqiDimension.CONFORMANCE)
                .parametersJson("{\"expression\":\"Patient.name.exists()\"}")
                .severityOverride(Severity.ERROR)
                .ordinal(0)
                .enabled(true)
                .build();
    }

    @Test
    @DisplayName("from() + toVersionEntity() carries every field the evaluate path reads, including scoringPolicyJson")
    void versionRoundTripCarriesEvaluateFields() {
        RubricVersion restored = RubricVersionSnapshot.from(version(), List.of(check())).toVersionEntity();

        assertThat(restored.getRubricVersionId()).isEqualTo(versionId);
        assertThat(restored.getRubricId()).isEqualTo("piqi.core");
        assertThat(restored.getSemver()).isEqualTo("1.2.0");
        assertThat(restored.getStatus()).isEqualTo(RubricVersionStatus.PUBLISHED);
        assertThat(restored.getChecksum()).isEqualTo("abc123");
        // the assembler scores with this — a snapshot that drops it silently falls back
        // to the default dimension/worst-of policy for every rubric
        assertThat(restored.getScoringPolicyJson()).isEqualTo(POLICY_JSON);
    }

    @Test
    @DisplayName("from() + toCheckEntities() carries every check field the executors read")
    void checkRoundTripCarriesAllFields() {
        List<RubricCheck> restored = RubricVersionSnapshot.from(version(), List.of(check())).toCheckEntities();

        assertThat(restored).hasSize(1);
        RubricCheck c = restored.get(0);
        assertThat(c.getCheckId()).isEqualTo(checkId);
        assertThat(c.getRubricVersionId()).isEqualTo(versionId);
        assertThat(c.getCheckLocalId()).isEqualTo("c1");
        assertThat(c.getType()).isEqualTo(CheckType.FHIRPATH);
        assertThat(c.getDimension()).isEqualTo(PiqiDimension.CONFORMANCE);
        assertThat(c.getParametersJson()).isEqualTo("{\"expression\":\"Patient.name.exists()\"}");
        assertThat(c.getSeverityOverride()).isEqualTo(Severity.ERROR);
        assertThat(c.getOrdinal()).isZero();
        assertThat(c.isEnabled()).isTrue();
    }
}
