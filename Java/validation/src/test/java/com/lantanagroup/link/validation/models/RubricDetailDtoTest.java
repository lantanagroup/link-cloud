package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class RubricDetailDtoTest {

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void fromCopiesRubricFieldsAndAttachesGivenLatestSemverAndVersions() {
        OffsetDateTime createdAt = OffsetDateTime.now().minusDays(1);
        OffsetDateTime updatedAt = OffsetDateTime.now();
        Rubric rubric = Rubric.builder()
                .rubricId("piqi.core").title("PIQI Core").owner("qa")
                .createdAt(createdAt).updatedAt(updatedAt).build();
        List<RubricVersionSummaryDto> versions = List.of(RubricVersionSummaryDto.builder().semver("1.0.0").build());

        RubricDetailDto dto = RubricDetailDto.from(rubric, "1.0.0", versions);

        assertThat(dto.getRubricId()).isEqualTo("piqi.core");
        assertThat(dto.getTitle()).isEqualTo("PIQI Core");
        assertThat(dto.getOwner()).isEqualTo("qa");
        assertThat(dto.getLatestPublishedSemver()).isEqualTo("1.0.0");
        assertThat(dto.getVersions()).isSameAs(versions);
        assertThat(dto.getCreatedAt()).isEqualTo(createdAt);
        assertThat(dto.getUpdatedAt()).isEqualTo(updatedAt);
    }

    @Test
    void fromAllowsANullLatestPublishedSemverWhenNothingIsPublishedYet() {
        Rubric rubric = Rubric.builder().rubricId("piqi.core").title("PIQI Core").build();

        RubricDetailDto dto = RubricDetailDto.from(rubric, null, List.of());

        assertThat(dto.getLatestPublishedSemver()).isNull();
    }

    @Test
    void parseNodeReturnsNullForNullOrBlankJson() {
        assertThat(RubricDetailDto.parseNode(null, objectMapper)).isNull();
        assertThat(RubricDetailDto.parseNode("", objectMapper)).isNull();
        assertThat(RubricDetailDto.parseNode("   ", objectMapper)).isNull();
    }

    @Test
    void parseNodeReturnsNullForMalformedJsonInsteadOfThrowing() {
        assertThat(RubricDetailDto.parseNode("{ not valid json", objectMapper)).isNull();
    }

    @Test
    void parseNodeParsesValidJsonIntoATree() {
        JsonNode node = RubricDetailDto.parseNode("{\"a\":1}", objectMapper);

        assertThat(node).isNotNull();
        assertThat(node.path("a").asInt()).isEqualTo(1);
    }
}
