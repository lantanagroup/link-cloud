package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class RubricSummaryDtoTest {

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void fromCopiesRubricFieldsAndAttachesGivenVersions() {
        OffsetDateTime createdAt = OffsetDateTime.now().minusDays(1);
        OffsetDateTime updatedAt = OffsetDateTime.now();
        Rubric rubric = Rubric.builder()
                .rubricId("piqi.core").title("PIQI Core").owner("qa")
                .createdAt(createdAt).updatedAt(updatedAt).build();
        List<RubricVersionSummaryDto> versions = List.of(RubricVersionSummaryDto.builder().semver("1.0.0").build());

        RubricSummaryDto dto = RubricSummaryDto.from(rubric, versions);

        assertThat(dto.getRubricId()).isEqualTo("piqi.core");
        assertThat(dto.getTitle()).isEqualTo("PIQI Core");
        assertThat(dto.getOwner()).isEqualTo("qa");
        assertThat(dto.getVersions()).isSameAs(versions);
        assertThat(dto.getCreatedAt()).isEqualTo(createdAt);
        assertThat(dto.getUpdatedAt()).isEqualTo(updatedAt);
    }

    @Test
    void parseDimensionsReturnsNullForNullOrBlankJson() {
        assertThat(RubricSummaryDto.parseDimensions(null, objectMapper)).isNull();
        assertThat(RubricSummaryDto.parseDimensions("", objectMapper)).isNull();
        assertThat(RubricSummaryDto.parseDimensions("   ", objectMapper)).isNull();
    }

    @Test
    void parseDimensionsReturnsNullForMalformedJsonInsteadOfThrowing() {
        assertThat(RubricSummaryDto.parseDimensions("[ not valid json", objectMapper)).isNull();
    }

    @Test
    void parseDimensionsParsesAValidArray() {
        List<PiqiDimension> dimensions =
                RubricSummaryDto.parseDimensions("[\"CONFORMANCE\",\"TERMINOLOGY\"]", objectMapper);

        assertThat(dimensions).containsExactly(PiqiDimension.CONFORMANCE, PiqiDimension.TERMINOLOGY);
    }
}
