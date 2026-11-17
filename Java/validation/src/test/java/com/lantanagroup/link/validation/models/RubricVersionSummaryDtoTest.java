package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;

import static org.assertj.core.api.Assertions.assertThat;

class RubricVersionSummaryDtoTest {

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void fromCopiesScalarFieldsAndParsesTheJsonColumns() {
        OffsetDateTime createdAt = OffsetDateTime.now();
        RubricVersion version = RubricVersion.builder()
                .rubricVersionId(1L)
                .rubricId("piqi.core")
                .semver("1.0.0")
                .status(RubricVersionStatus.PUBLISHED)
                .checksum("abc123")
                .dimensionsJson("[\"CONFORMANCE\"]")
                .applicableContextJson("{\"fhirResources\":[\"Patient\"]}")
                .scoringPolicyJson("{\"type\":\"piqi-dimension-scorecard\",\"rollup\":\"worst-of\"}")
                .createdAt(createdAt)
                .createdBy("qa")
                .build();

        RubricVersionSummaryDto dto = RubricVersionSummaryDto.from(version, objectMapper);

        assertThat(dto.getRubricVersionId()).isEqualTo(1L);
        assertThat(dto.getRubricId()).isEqualTo("piqi.core");
        assertThat(dto.getSemver()).isEqualTo("1.0.0");
        assertThat(dto.getStatus()).isEqualTo(RubricVersionStatus.PUBLISHED);
        assertThat(dto.getChecksum()).isEqualTo("abc123");
        assertThat(dto.getDimensions()).containsExactly(PiqiDimension.CONFORMANCE);
        assertThat(dto.getApplicableContext().path("fhirResources").get(0).asText()).isEqualTo("Patient");
        assertThat(dto.getScoringPolicy().path("type").asText()).isEqualTo("piqi-dimension-scorecard");
        assertThat(dto.getCreatedAt()).isEqualTo(createdAt);
        assertThat(dto.getCreatedBy()).isEqualTo("qa");
    }

    @Test
    void fromToleratesNullJsonColumnsWithoutThrowing() {
        RubricVersion version = RubricVersion.builder()
                .rubricVersionId(1L).rubricId("piqi.core").semver("1.0.0")
                .status(RubricVersionStatus.DRAFT).checksum("abc123")
                .build();

        RubricVersionSummaryDto dto = RubricVersionSummaryDto.from(version, objectMapper);

        assertThat(dto.getDimensions()).isNull();
        assertThat(dto.getApplicableContext()).isNull();
        assertThat(dto.getScoringPolicy()).isNull();
    }
}
