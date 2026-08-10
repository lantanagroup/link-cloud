package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.SubjectDto;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class ResultEnvelopeAssemblerTest {

    private final ResultEnvelopeAssembler assembler =
            new ResultEnvelopeAssembler(new ObjectMapper(), new ScoreAggregator());

    private static RawFinding finding(UUID checkId, PiqiDimension dimension, Severity severity, String code) {
        return RawFinding.builder()
                .checkId(checkId)
                .checkLocalId("c-" + code)
                .dimension(dimension)
                .severity(severity)
                .code(code)
                .message(code + " message")
                .location("Patient/p1")
                .build();
    }

    private static RubricVersion version() {
        return RubricVersion.builder()
                .rubricId("piqi.core")
                .semver("1.3.0")
                .rubricVersionId(UUID.randomUUID())
                .checksum("checksum-abc")
                .build();
    }

    @Test
    @DisplayName("assembles envelope + persisted entities from findings, with summary counts and duration")
    void assemblesEnvelopeAndEntities() {
        OffsetDateTime requestedAt = OffsetDateTime.now().minusSeconds(5);
        OffsetDateTime completedAt = requestedAt.plusNanos(40_000_000L); // +40ms

        ExecutionContext ctx = ExecutionContext.builder()
                .requestId(UUID.randomUUID())
                .correlationId("corr-1")
                .requestor("tester")
                .subject(SubjectDto.builder().facilityId("f1").patientId("p1").build())
                .requestedAt(requestedAt)
                .build();

        UUID checkE1 = UUID.randomUUID();
        UUID checkW1 = UUID.randomUUID();
        UUID checkI1 = UUID.randomUUID();
        List<RawFinding> raw = List.of(
                finding(checkE1, PiqiDimension.CONFORMANCE, Severity.ERROR, "e1"),
                finding(checkW1, PiqiDimension.TERMINOLOGY, Severity.WARNING, "w1"),
                finding(checkI1, PiqiDimension.COMPLETENESS, Severity.INFORMATION, "i1"));

        RubricVersion version = version();
        ResultEnvelopeAssembler.AssembleOutput out =
                assembler.assemble(ctx, version, raw, List.of(), Map.of("c-e1", 12L), completedAt);

        // envelope
        assertThat(out.envelope().getRubricId()).isEqualTo("piqi.core");
        assertThat(out.envelope().getRubricVersion()).isEqualTo("1.3.0");
        assertThat(out.envelope().getRubricVersionHash()).isEqualTo("checksum-abc");
        assertThat(out.envelope().getStatus()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(out.envelope().getFindings()).hasSize(3);
        assertThat(out.envelope().getSummary().getErrorCount()).isEqualTo(1);
        assertThat(out.envelope().getSummary().getWarningCount()).isEqualTo(1);
        assertThat(out.envelope().getSummary().getInformationCount()).isEqualTo(1);
        assertThat(out.envelope().getTrace().getDurationMs()).isEqualTo(40L);
        assertThat(out.envelope().getTrace().getRequestedAt()).isEqualTo(requestedAt);

        // persisted result entity
        assertThat(out.resultEntity().getRubricId()).isEqualTo("piqi.core");
        assertThat(out.resultEntity().getRubricVersionId()).isEqualTo(version.getRubricVersionId());
        assertThat(out.resultEntity().getStatus()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(out.resultEntity().getErrorCount()).isEqualTo(1);
        assertThat(out.resultEntity().getFacilityId()).isEqualTo("f1");
        assertThat(out.resultEntity().getDurationMs()).isEqualTo(40L);

        // one finding entity per raw finding, all pointing at the result
        assertThat(out.findingEntities()).hasSize(3);
        assertThat(out.findingEntities()).allSatisfy(f ->
                assertThat(f.getResultId()).isEqualTo(out.resultEntity().getResultId()));

        // the persisted entity carries the real rubric_check.check_id FK (regression: was UUID.randomUUID())
        assertThat(out.findingEntities()).extracting(RubricFinding::getCheckId)
                .containsExactly(checkE1, checkW1, checkI1);
        // ...while the API DTO exposes the stable, human-facing check local id
        assertThat(out.envelope().getFindings()).extracting(FindingDto::getCheckId)
                .containsExactly("c-e1", "c-w1", "c-i1");
    }

    @Test
    @DisplayName("no findings -> ACCEPTABLE envelope with zero counts and no finding entities")
    void noFindingsIsAcceptable() {
        ExecutionContext ctx = ExecutionContext.builder()
                .requestId(UUID.randomUUID())
                .requestedAt(OffsetDateTime.now())
                .build();

        ResultEnvelopeAssembler.AssembleOutput out =
                assembler.assemble(ctx, version(), List.of(), List.of(), Map.of(), OffsetDateTime.now());

        assertThat(out.envelope().getStatus()).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(out.envelope().getFindings()).isEmpty();
        assertThat(out.envelope().getSummary().getErrorCount()).isZero();
        assertThat(out.findingEntities()).isEmpty();
    }

    @Test
    @DisplayName("a null subject does not NPE; facility/patient fields stay null")
    void nullSubjectIsSafe() {
        ExecutionContext ctx = ExecutionContext.builder()
                .requestId(UUID.randomUUID())
                .requestedAt(OffsetDateTime.now())
                .build();

        ResultEnvelopeAssembler.AssembleOutput out =
                assembler.assemble(ctx, version(), List.of(), List.of(), Map.of(), OffsetDateTime.now());

        assertThat(out.resultEntity().getFacilityId()).isNull();
        assertThat(out.resultEntity().getPatientId()).isNull();
    }
}
