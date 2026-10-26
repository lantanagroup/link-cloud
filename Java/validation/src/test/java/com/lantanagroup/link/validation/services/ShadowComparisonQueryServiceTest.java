package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.models.ShadowComparisonResultDto;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import com.lantanagroup.link.validation.records.ShadowSeverityChangeDto;
import com.lantanagroup.link.validation.repositories.ShadowComparisonResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class ShadowComparisonQueryServiceTest {

    private final ShadowComparisonResultRepository repository = mock(ShadowComparisonResultRepository.class);
    private final ObjectMapper objectMapper = new ObjectMapper();

    private final ShadowComparisonQueryService service =
            new ShadowComparisonQueryService(repository, objectMapper);

    @Test
    @DisplayName("unknown request id -> empty list (controller turns this into a 404)")
    void unknownRequestIdIsEmpty() {
        UUID requestId = UUID.randomUUID();
        when(repository.findByRequestIdOrderByComparedAtDesc(requestId)).thenReturn(List.of());

        assertThat(service.findByRequestId(requestId)).isEmpty();
    }

    @Test
    @DisplayName("maps entity -> DTO: parses added/missing/severity-changed json into structured findings")
    void mapsMismatchToDto() throws Exception {
        UUID requestId = UUID.randomUUID();
        UUID comparisonId = UUID.randomUUID();
        OffsetDateTime comparedAt = OffsetDateTime.now();

        ShadowFindingDto added = ShadowFindingDto.builder()
                .severity(OperationOutcome.IssueSeverity.WARNING)
                .code(OperationOutcome.IssueType.INVALID)
                .location("new-only")
                .build();
        ShadowFindingDto missing = ShadowFindingDto.builder()
                .severity(OperationOutcome.IssueSeverity.ERROR)
                .code(OperationOutcome.IssueType.INVALID)
                .location("legacy-only")
                .build();
        ShadowSeverityChangeDto severityChange = ShadowSeverityChangeDto.builder()
                .legacy(missing)
                .modern(added)
                .build();

        ShadowComparisonResult entity = ShadowComparisonResult.builder()
                .id(comparisonId)
                .requestId(requestId)
                .correlationId("corr-1")
                .facilityId("facility-1")
                .patientId("patient-1")
                .reportId("report-1")
                .rubricId("rubric-1")
                .ranNewEngine(true)
                .matched(false)
                .addedCount(1)
                .missingCount(1)
                .severityChangedCount(1)
                .matchedFindingCount(0)
                .addedJson(objectMapper.writeValueAsString(List.of(added)))
                .missingJson(objectMapper.writeValueAsString(List.of(missing)))
                .severityChangedJson(objectMapper.writeValueAsString(List.of(severityChange)))
                .comparedAt(comparedAt)
                .build();

        when(repository.findByRequestIdOrderByComparedAtDesc(requestId)).thenReturn(List.of(entity));

        List<ShadowComparisonResultDto> results = service.findByRequestId(requestId);

        assertThat(results).hasSize(1);
        ShadowComparisonResultDto dto = results.get(0);
        assertThat(dto.getRequestId()).isEqualTo(requestId);
        assertThat(dto.isMatched()).isFalse();
        assertThat(dto.getAdded()).hasSize(1);
        assertThat(dto.getAdded().get(0).getLocation()).isEqualTo("new-only");
        assertThat(dto.getMissing()).hasSize(1);
        assertThat(dto.getMissing().get(0).getLocation()).isEqualTo("legacy-only");
        assertThat(dto.getSeverityChanged()).hasSize(1);
        assertThat(dto.getSeverityChanged().get(0).getLegacy().getLocation()).isEqualTo("legacy-only");
        assertThat(dto.getSeverityChanged().get(0).getModern().getLocation()).isEqualTo("new-only");
        assertThat(dto.getComparedAt()).isEqualTo(comparedAt);
    }

    @Test
    @DisplayName("a matched comparison with null json columns maps to empty finding lists, not a parse failure")
    void nullJsonColumnsMapToEmptyLists() {
        UUID requestId = UUID.randomUUID();
        ShadowComparisonResult entity = ShadowComparisonResult.builder()
                .id(UUID.randomUUID())
                .requestId(requestId)
                .facilityId("facility-1")
                .patientId("patient-1")
                .reportId("report-1")
                .matched(true)
                .comparedAt(OffsetDateTime.now())
                .build();
        when(repository.findByRequestIdOrderByComparedAtDesc(requestId)).thenReturn(List.of(entity));

        ShadowComparisonResultDto dto = service.findByRequestId(requestId).get(0);

        assertThat(dto.getAdded()).isEmpty();
        assertThat(dto.getMissing()).isEmpty();
        assertThat(dto.getSeverityChanged()).isEmpty();
    }

    @Test
    @DisplayName("malformed json is swallowed as an empty list rather than failing the whole lookup")
    void malformedJsonIsSwallowed() {
        UUID requestId = UUID.randomUUID();
        ShadowComparisonResult entity = ShadowComparisonResult.builder()
                .id(UUID.randomUUID())
                .requestId(requestId)
                .facilityId("facility-1")
                .patientId("patient-1")
                .reportId("report-1")
                .matched(false)
                .addedJson("not-json")
                .comparedAt(OffsetDateTime.now())
                .build();
        when(repository.findByRequestIdOrderByComparedAtDesc(requestId)).thenReturn(List.of(entity));

        ShadowComparisonResultDto dto = service.findByRequestId(requestId).get(0);

        assertThat(dto.getAdded()).isEmpty();
    }
}
