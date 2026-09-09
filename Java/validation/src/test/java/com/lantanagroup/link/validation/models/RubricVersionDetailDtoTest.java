package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.enums.Severity;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class RubricVersionDetailDtoTest {

    private final ObjectMapper objectMapper = new ObjectMapper();

    private static RubricVersion version() {
        return RubricVersion.builder()
                .rubricVersionId(1L)
                .rubricId("piqi.core")
                .semver("1.0.0")
                .status(RubricVersionStatus.PUBLISHED)
                .checksum("abc123")
                .definitionJson("{\"id\":\"piqi.core\"}")
                .build();
    }

    @Test
    void fromCopiesVersionScalarFieldsAndParsesTheDefinitionJson() {
        RubricVersionDetailDto dto = RubricVersionDetailDto.from(version(), List.of(), objectMapper);

        assertThat(dto.getRubricVersionId()).isEqualTo(1L);
        assertThat(dto.getRubricId()).isEqualTo("piqi.core");
        assertThat(dto.getSemver()).isEqualTo("1.0.0");
        assertThat(dto.getStatus()).isEqualTo(RubricVersionStatus.PUBLISHED);
        assertThat(dto.getChecksum()).isEqualTo("abc123");
        assertThat(dto.getDefinition().path("id").asText()).isEqualTo("piqi.core");
    }

    @Test
    void fromMapsEachCheckIncludingItsParsedParameters() {
        RubricCheck check = RubricCheck.builder()
                .checkLocalId("c1")
                .type(CheckType.FHIRPATH)
                .dimension(PiqiDimension.CONFORMANCE)
                .parametersJson("{\"expression\":\"Patient.name.exists()\"}")
                .severityOverride(Severity.ERROR)
                .ordinal(0)
                .enabled(true)
                .build();

        RubricVersionDetailDto dto = RubricVersionDetailDto.from(version(), List.of(check), objectMapper);

        assertThat(dto.getChecks()).hasSize(1);
        CheckDto checkDto = dto.getChecks().get(0);
        assertThat(checkDto.getId()).isEqualTo("c1");
        assertThat(checkDto.getType()).isEqualTo(CheckType.FHIRPATH);
        assertThat(checkDto.getDimension()).isEqualTo(PiqiDimension.CONFORMANCE);
        assertThat(checkDto.getParameters().path("expression").asText()).isEqualTo("Patient.name.exists()");
        assertThat(checkDto.getSeverityOverride()).isEqualTo(Severity.ERROR);
        assertThat(checkDto.getOrdinal()).isEqualTo(0);
        assertThat(checkDto.isEnabled()).isTrue();
    }

    @Test
    void fromToleratesACheckWithNoParametersJson() {
        RubricCheck check = RubricCheck.builder()
                .checkLocalId("c1").type(CheckType.CUSTOM).dimension(PiqiDimension.CONFORMANCE)
                .enabled(true).build();

        RubricVersionDetailDto dto = RubricVersionDetailDto.from(version(), List.of(check), objectMapper);

        assertThat(dto.getChecks().get(0).getParameters()).isNull();
    }
}
