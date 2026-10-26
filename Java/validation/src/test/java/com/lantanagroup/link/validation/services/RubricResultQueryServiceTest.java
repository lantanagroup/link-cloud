package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricResult;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RubricResultDto;
import com.lantanagroup.link.validation.models.ScoreCardDto;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricFindingRepository;
import com.lantanagroup.link.validation.repositories.RubricResultRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.EnumMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class RubricResultQueryServiceTest {

    private final RubricResultRepository resultRepository = mock(RubricResultRepository.class);
    private final RubricFindingRepository findingRepository = mock(RubricFindingRepository.class);
    private final RubricCheckRepository checkRepository = mock(RubricCheckRepository.class);
    private final ObjectMapper objectMapper = new ObjectMapper();

    private final RubricResultQueryService service =
            new RubricResultQueryService(resultRepository, findingRepository, checkRepository, objectMapper);

    @Test
    @DisplayName("unknown request id -> empty Optional (controller turns this into a 404)")
    void unknownRequestIdIsEmpty() {
        UUID requestId = UUID.randomUUID();
        when(resultRepository.findByRequestId(requestId)).thenReturn(Optional.empty());

        assertThat(service.findByRequestId(requestId)).isEmpty();
    }

    @Test
    @DisplayName("maps entity -> DTO: parses score_json, rebuilds subject/summary/trace, resolves check local ids")
    void mapsResultToDto() throws Exception {
        UUID requestId = UUID.randomUUID();
        Long resultId = 1L;
        Long checkId = 100L;

        Map<PiqiDimension, RubricResultStatus> byDim = new EnumMap<>(PiqiDimension.class);
        byDim.put(PiqiDimension.CONFORMANCE, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        ScoreCardDto score = ScoreCardDto.builder()
                .interpretation(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)
                .byDimension(byDim)
                .value(0.75)
                .build();

        RubricResult result = RubricResult.builder()
                .resultId(resultId)
                .requestId(requestId)
                .rubricId("piqi.core")
                .rubricVersionId(10L)
                .status(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)
                .scoreJson(objectMapper.writeValueAsString(score))
                .errorCount(0)
                .warningCount(1)
                .informationCount(0)
                .correlationId("corr-1")
                .requestor("tester")
                .facilityId("f1")
                .patientId("p1")
                .requestedAt(OffsetDateTime.now().minusSeconds(1))
                .completedAt(OffsetDateTime.now())
                .durationMs(42L)
                .build();

        RubricFinding finding = RubricFinding.builder()
                .findingId(1L)
                .resultId(resultId)
                .checkId(checkId)
                .dimension(PiqiDimension.CONFORMANCE)
                .severity(Severity.WARNING)
                .code("terminology-code-invalid")
                .message("bad code")
                .location("Observation/o1")
                .expression("sys|code")
                .build();

        RubricCheck check = RubricCheck.builder()
                .checkId(checkId)
                .checkLocalId("chk-term-1")
                .build();

        when(resultRepository.findByRequestId(requestId)).thenReturn(Optional.of(result));
        when(findingRepository.findByResultId(resultId)).thenReturn(List.of(finding));
        when(checkRepository.findAllById(any())).thenReturn(List.of(check));

        RubricResultDto dto = service.findByRequestId(requestId).orElseThrow();

        assertThat(dto.getRubricId()).isEqualTo("piqi.core");
        assertThat(dto.getRequestId()).isEqualTo(requestId);
        assertThat(dto.getStatus()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        // score_json parsed back into a nested object, not an escaped string
        assertThat(dto.getScore()).isNotNull();
        assertThat(dto.getScore().getValue()).isEqualTo(0.75);
        assertThat(dto.getScore().getByDimension())
                .containsEntry(PiqiDimension.CONFORMANCE, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        // summary from the flat counts
        assertThat(dto.getSummary().getWarningCount()).isEqualTo(1);
        // subject rebuilt from the flat columns
        assertThat(dto.getSubject().getFacilityId()).isEqualTo("f1");
        assertThat(dto.getSubject().getPatientId()).isEqualTo("p1");
        // trace timing
        assertThat(dto.getTrace().getDurationMs()).isEqualTo(42L);
        // finding's stored check_id UUID resolved to the human-facing local id (matches $rubric-validate)
        assertThat(dto.getFindings()).hasSize(1);
        assertThat(dto.getFindings().get(0).getCheckId()).isEqualTo("chk-term-1");
        assertThat(dto.getFindings().get(0).getCode()).isEqualTo("terminology-code-invalid");
    }

    @Test
    @DisplayName("a null subject (no facility/patient/etc.) is omitted rather than emitted as an empty object")
    void nullSubjectOmitted() {
        UUID requestId = UUID.randomUUID();
        Long resultId = 2L;
        RubricResult result = RubricResult.builder()
                .resultId(resultId)
                .requestId(requestId)
                .rubricId("piqi.core")
                .status(RubricResultStatus.ACCEPTABLE)
                .requestedAt(OffsetDateTime.now())
                .completedAt(OffsetDateTime.now())
                .durationMs(1L)
                .build();
        when(resultRepository.findByRequestId(requestId)).thenReturn(Optional.of(result));
        when(findingRepository.findByResultId(eq(resultId))).thenReturn(List.of());

        RubricResultDto dto = service.findByRequestId(requestId).orElseThrow();

        assertThat(dto.getSubject()).isNull();
        assertThat(dto.getScore()).isNull();
        assertThat(dto.getFindings()).isEmpty();
    }
}
