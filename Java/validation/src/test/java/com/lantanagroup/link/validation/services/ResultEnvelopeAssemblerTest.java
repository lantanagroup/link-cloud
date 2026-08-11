package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.SubjectDto;
import com.lantanagroup.link.validation.services.execution.CheckExecutionResult;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import com.lantanagroup.link.validation.services.scoring.FindingStatusResolver;
import com.lantanagroup.link.validation.services.scoring.ScoringPolicyResolver;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class ResultEnvelopeAssemblerTest {

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final ValidationPolicyConfig policyConfig = new ValidationPolicyConfig();
    private final ResultEnvelopeAssembler assembler = new ResultEnvelopeAssembler(
            objectMapper, new ScoreAggregator(new FindingStatusResolver()),
            new ScoringPolicyResolver(policyConfig, objectMapper), policyConfig);

    /**
     * Wraps raw findings as uncategorized, which is what the assembler receives whenever category
     * override is disabled — so the pre-override envelope shape stays pinned by these cases.
     */
    private static List<EvaluatedFinding> identity(List<RawFinding> raw) {
        return raw.stream().map(EvaluatedFinding::identity).toList();
    }

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

        // Per-check results the dimension scorecard scores from — findings alone no longer drive
        // scoring, so an assembler test must supply the check results that mirror the findings.
        List<CheckExecutionResult> checkResults = List.of(
                CheckExecutionResult.builder().checkLocalId("c-e1").dimension(PiqiDimension.CONFORMANCE).status(RubricResultStatus.UNACCEPTABLE).build(),
                CheckExecutionResult.builder().checkLocalId("c-w1").dimension(PiqiDimension.TERMINOLOGY).status(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS).build(),
                CheckExecutionResult.builder().checkLocalId("c-i1").dimension(PiqiDimension.COMPLETENESS).status(RubricResultStatus.ACCEPTABLE).build());

        RubricVersion version = version();
        ResultEnvelopeAssembler.AssembleOutput out =
                assembler.assemble(ctx, version, identity(raw), checkResults, Map.of("c-e1", 12L), completedAt);

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
                assembler.assemble(ctx, version(), identity(List.of()), List.of(), Map.of(), OffsetDateTime.now());

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
                assembler.assemble(ctx, version(), identity(List.of()), List.of(), Map.of(), OffsetDateTime.now());

        assertThat(out.resultEntity().getFacilityId()).isNull();
        assertThat(out.resultEntity().getPatientId()).isNull();
    }

    // ------------------------------------------------------------------
    // Category-override decorations
    // ------------------------------------------------------------------

    private static EvaluatedFinding categorized(RawFinding raw, Severity effective, boolean acceptable) {
        return new EvaluatedFinding(raw, raw.getSeverity(), effective, acceptable,
                List.of("cat-1"), "cat-1");
    }

    private ExecutionContext ctx() {
        return ExecutionContext.builder()
                .requestId(UUID.randomUUID())
                .requestedAt(OffsetDateTime.now())
                .build();
    }

    @Test
    @DisplayName("a categorized finding exposes original/overridden severity, acceptability, and category ids in the DTO")
    void categorizedFindingCarriesOverrideFields() {
        RawFinding raw = finding(UUID.randomUUID(), PiqiDimension.CONFORMANCE, Severity.ERROR, "e1");

        ResultEnvelopeAssembler.AssembleOutput out = assembler.assemble(
                ctx(), version(), List.of(categorized(raw, Severity.WARNING, true)),
                List.of(), Map.of(), OffsetDateTime.now());

        FindingDto dto = out.envelope().getFindings().get(0);
        assertThat(dto.getSeverity()).isEqualTo(Severity.WARNING);
        assertThat(dto.getOriginalSeverity()).isEqualTo(Severity.ERROR);
        assertThat(dto.getOverriddenSeverity()).isEqualTo(Severity.WARNING);
        assertThat(dto.getAcceptable()).isTrue();
        assertThat(dto.getCategoryIds()).containsExactly("cat-1");
        assertThat(dto.getGoverningCategoryId()).isEqualTo("cat-1");
    }

    @Test
    @DisplayName("summary counts stay on the pre-override severities while the persisted finding stores the effective one")
    void summaryCountsPreOverrideAndEntityStoresEffective() {
        RawFinding raw = finding(UUID.randomUUID(), PiqiDimension.CONFORMANCE, Severity.ERROR, "e1");

        ResultEnvelopeAssembler.AssembleOutput out = assembler.assemble(
                ctx(), version(), List.of(categorized(raw, Severity.WARNING, true)),
                List.of(), Map.of(), OffsetDateTime.now());

        assertThat(out.envelope().getSummary().getErrorCount()).isEqualTo(1);
        assertThat(out.envelope().getSummary().getWarningCount()).isZero();
        assertThat(out.findingEntities().get(0).getSeverity()).isEqualTo(Severity.WARNING);
    }

    @Test
    @DisplayName("an uncategorized finding emits no override diagnostics at all")
    void uncategorizedFindingHasNoOverrideFields() {
        RawFinding raw = finding(UUID.randomUUID(), PiqiDimension.CONFORMANCE, Severity.ERROR, "e1");

        ResultEnvelopeAssembler.AssembleOutput out = assembler.assemble(
                ctx(), version(), identity(List.of(raw)), List.of(), Map.of(), OffsetDateTime.now());

        FindingDto dto = out.envelope().getFindings().get(0);
        assertThat(dto.getOriginalSeverity()).isNull();
        assertThat(dto.getOverriddenSeverity()).isNull();
        assertThat(dto.getAcceptable()).isNull();
        assertThat(dto.getGoverningCategoryId()).isNull();
    }

    @Test
    @DisplayName("response config can suppress the diagnostic fields without touching the effective severity")
    void responseConfigSuppressesDiagnostics() {
        policyConfig.getResponse().setIncludeOriginalSeverity(false);
        policyConfig.getResponse().setIncludeCategoryIds(false);
        RawFinding raw = finding(UUID.randomUUID(), PiqiDimension.CONFORMANCE, Severity.ERROR, "e1");

        ResultEnvelopeAssembler.AssembleOutput out = assembler.assemble(
                ctx(), version(), List.of(categorized(raw, Severity.WARNING, true)),
                List.of(), Map.of(), OffsetDateTime.now());

        FindingDto dto = out.envelope().getFindings().get(0);
        assertThat(dto.getSeverity()).isEqualTo(Severity.WARNING);
        assertThat(dto.getOriginalSeverity()).isNull();
        assertThat(dto.getCategoryIds()).isNull();
        assertThat(dto.getAcceptable()).isTrue();
    }
}
