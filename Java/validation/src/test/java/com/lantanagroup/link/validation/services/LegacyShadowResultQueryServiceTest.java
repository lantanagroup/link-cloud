package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.LegacyShadowFinding;
import com.lantanagroup.link.validation.entities.LegacyShadowResult;
import com.lantanagroup.link.validation.models.LegacyShadowResultDto;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import com.lantanagroup.link.validation.repositories.LegacyShadowFindingRepository;
import com.lantanagroup.link.validation.repositories.LegacyShadowResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class LegacyShadowResultQueryServiceTest {

    private final LegacyShadowResultRepository resultRepository = mock(LegacyShadowResultRepository.class);
    private final LegacyShadowFindingRepository findingRepository = mock(LegacyShadowFindingRepository.class);
    private final ObjectMapper objectMapper = new ObjectMapper();

    private final LegacyShadowResultQueryService service =
            new LegacyShadowResultQueryService(resultRepository, findingRepository, objectMapper);

    @Test
    @DisplayName("unknown request id -> empty Optional")
    void unknownRequestIdIsEmpty() {
        UUID requestId = UUID.randomUUID();
        when(resultRepository.findFirstByRequestIdOrderByRequestedAtDesc(requestId)).thenReturn(Optional.empty());

        assertThat(service.findByRequestId(requestId)).isEmpty();
    }

    @Test
    @DisplayName("maps entity + findings -> DTO, parsing category_ids_json back into a list")
    void mapsResultToDto() throws Exception {
        UUID requestId = UUID.randomUUID();
        UUID resultId = UUID.randomUUID();

        LegacyShadowResult result = LegacyShadowResult.builder()
                .resultId(resultId)
                .requestId(requestId)
                .correlationId("corr-1")
                .facilityId("f1")
                .patientId("p1")
                .reportId("report-1")
                .fatalCount(0)
                .errorCount(1)
                .warningCount(2)
                .informationCount(0)
                .requestedAt(OffsetDateTime.now().minusSeconds(1))
                .completedAt(OffsetDateTime.now())
                .durationMs(42L)
                .build();

        LegacyShadowFinding finding = LegacyShadowFinding.builder()
                .findingId(UUID.randomUUID())
                .resultId(resultId)
                .requestId(requestId)
                .severity(OperationOutcome.IssueSeverity.ERROR)
                .code(OperationOutcome.IssueType.INVALID)
                .message("bad value")
                .location("Observation/o1")
                .expression("sys|code")
                .categoryIdsJson(objectMapper.writeValueAsString(List.of("cat-1", "cat-2")))
                .acceptable(false)
                .build();

        when(resultRepository.findFirstByRequestIdOrderByRequestedAtDesc(requestId)).thenReturn(Optional.of(result));
        when(findingRepository.findByRequestId(requestId)).thenReturn(List.of(finding));

        LegacyShadowResultDto dto = service.findByRequestId(requestId).orElseThrow();

        assertThat(dto.getResultId()).isEqualTo(resultId);
        assertThat(dto.getReportId()).isEqualTo("report-1");
        assertThat(dto.getErrorCount()).isEqualTo(1);
        assertThat(dto.getWarningCount()).isEqualTo(2);
        assertThat(dto.getFindings()).hasSize(1);

        ShadowFindingDto findingDto = dto.getFindings().get(0);
        assertThat(findingDto.getMessage()).isEqualTo("bad value");
        assertThat(findingDto.getSeverity()).isEqualTo(OperationOutcome.IssueSeverity.ERROR);
        assertThat(findingDto.getAcceptable()).isFalse();
        assertThat(findingDto.getCategoryIds()).containsExactly("cat-1", "cat-2");
    }

    @Test
    @DisplayName("no category_ids_json -> empty list rather than null or an error")
    void blankCategoryIdsJsonYieldsEmptyList() {
        UUID requestId = UUID.randomUUID();
        UUID resultId = UUID.randomUUID();

        LegacyShadowResult result = LegacyShadowResult.builder()
                .resultId(resultId)
                .requestId(requestId)
                .facilityId("f1")
                .patientId("p1")
                .reportId("report-1")
                .requestedAt(OffsetDateTime.now())
                .completedAt(OffsetDateTime.now())
                .durationMs(1L)
                .build();

        LegacyShadowFinding finding = LegacyShadowFinding.builder()
                .findingId(UUID.randomUUID())
                .resultId(resultId)
                .requestId(requestId)
                .severity(OperationOutcome.IssueSeverity.WARNING)
                .code(OperationOutcome.IssueType.STRUCTURE)
                .message("missing extension")
                .build();

        when(resultRepository.findFirstByRequestIdOrderByRequestedAtDesc(requestId)).thenReturn(Optional.of(result));
        when(findingRepository.findByRequestId(requestId)).thenReturn(List.of(finding));

        LegacyShadowResultDto dto = service.findByRequestId(requestId).orElseThrow();

        assertThat(dto.getFindings()).hasSize(1);
        assertThat(dto.getFindings().get(0).getCategoryIds()).isEmpty();
    }
}
