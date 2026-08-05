package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricResult;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.EvaluateRequestDto;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.SubjectDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import com.lantanagroup.link.validation.services.execution.CheckExecutorRegistry;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.ArgumentMatchers.anyMap;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class RubricExecutionServiceTest {

    private final RubricVersionResolver resolver = mock(RubricVersionResolver.class);
    private final RubricCheckRepository checkRepository = mock(RubricCheckRepository.class);
    private final RubricVersionRepository versionRepository = mock(RubricVersionRepository.class);
    private final CheckExecutorRegistry registry = mock(CheckExecutorRegistry.class);
    private final ResultEnvelopeAssembler assembler = mock(ResultEnvelopeAssembler.class);
    private final RubricResultPersister resultPersister = mock(RubricResultPersister.class);
    private final FhirContext fhirContext = FhirContext.forR4();
    private final ObjectMapper objectMapper = new ObjectMapper();

    private final RubricExecutionService service = new RubricExecutionService(
            resolver, checkRepository, versionRepository, registry, assembler,
            resultPersister, fhirContext, objectMapper);

    private final UUID versionId = UUID.randomUUID();
    private final RubricVersion version = RubricVersion.builder()
            .rubricId("piqi.core").semver("1.0.0").rubricVersionId(versionId).checksum("abc").build();

    private EvaluateRequestDto request() throws Exception {
        JsonNode payload = objectMapper.readTree("{\"resourceType\":\"Patient\",\"id\":\"p1\"}");
        return EvaluateRequestDto.builder()
                .subject(SubjectDto.builder().facilityId("f1").build())
                .payload(payload)
                .build();
    }

    private static RubricCheck check(boolean enabled) {
        return RubricCheck.builder()
                .checkId(UUID.randomUUID())
                .checkLocalId("c1")
                .type(CheckType.FHIRPATH)
                .dimension(PiqiDimension.CONFORMANCE)
                .enabled(enabled)
                .build();
    }

    private ResultEnvelopeAssembler.AssembleOutput stubAssembler() {
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder().requestId(UUID.randomUUID()).build();
        RubricResult resultEntity = RubricResult.builder()
                .resultId(UUID.randomUUID())
                .status(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)
                .build();
        List<RubricFinding> findingEntities = List.of(RubricFinding.builder().findingId(UUID.randomUUID()).build());
        ResultEnvelopeAssembler.AssembleOutput out =
                new ResultEnvelopeAssembler.AssembleOutput(envelope, resultEntity, findingEntities);
        when(assembler.assemble(any(ExecutionContext.class), eq(version), anyList(), anyMap(), any(OffsetDateTime.class)))
                .thenReturn(out);
        return out;
    }

    @Test
    @DisplayName("evaluate(persist=true) runs checks, assembles, and persists result + findings")
    void evaluatePersistsWhenRequested() throws Exception {
        when(resolver.resolve("piqi.core", "1.0.0", true)).thenReturn(version);
        RubricCheck c = check(true);
        when(checkRepository.findByRubricVersionIdOrderByOrdinalAsc(versionId)).thenReturn(List.of(c));
        CheckExecutor executor = mock(CheckExecutor.class);
        when(executor.execute(eq(c), any(ExecutionContext.class)))
                .thenReturn(List.of(RawFinding.builder().checkLocalId("c1").dimension(PiqiDimension.CONFORMANCE)
                        .severity(Severity.ERROR).code("x").build()));
        when(registry.get(CheckType.FHIRPATH)).thenReturn(executor);
        ResultEnvelopeAssembler.AssembleOutput out = stubAssembler();

        ValidationResultEnvelope result = service.evaluate("piqi.core", "1.0.0", request(), true);

        assertThat(result).isSameAs(out.envelope());
        verify(resultPersister).persist(out.resultEntity(), out.findingEntities());
        // a real evaluation must not masquerade as a dry run on the version
        verify(versionRepository, never()).recordDryRun(any(), any(), any());
    }

    @Test
    @DisplayName("evaluate(persist=false) is a dry run: no result row, but the outcome is recorded on the version")
    void dryRunDoesNotPersist() throws Exception {
        when(resolver.resolve("piqi.core", "1.0.0", false)).thenReturn(version);
        when(checkRepository.findByRubricVersionIdOrderByOrdinalAsc(versionId)).thenReturn(List.of());
        stubAssembler();

        service.evaluate("piqi.core", "1.0.0", request(), false);

        verify(resultPersister, never()).persist(any(), any());
        verify(versionRepository).recordDryRun(
                eq(versionId), eq(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS), any(OffsetDateTime.class));
    }

    @Test
    @DisplayName("disabled checks are skipped — no executor is resolved for them")
    void disabledChecksAreSkipped() throws Exception {
        when(resolver.resolve("piqi.core", "1.0.0", true)).thenReturn(version);
        when(checkRepository.findByRubricVersionIdOrderByOrdinalAsc(versionId)).thenReturn(List.of(check(false)));
        stubAssembler();

        service.evaluate("piqi.core", "1.0.0", request(), true);

        verify(registry, never()).get(any());
    }

    @Test
    @DisplayName("a check executor that throws yields a check-execution-error finding instead of failing")
    void executorFailureBecomesErrorFinding() throws Exception {
        when(resolver.resolve("piqi.core", "1.0.0", true)).thenReturn(version);
        RubricCheck c = check(true);
        when(checkRepository.findByRubricVersionIdOrderByOrdinalAsc(versionId)).thenReturn(List.of(c));
        CheckExecutor executor = mock(CheckExecutor.class);
        when(executor.execute(any(), any())).thenThrow(new RuntimeException("boom"));
        when(registry.get(CheckType.FHIRPATH)).thenReturn(executor);
        stubAssembler();

        service.evaluate("piqi.core", "1.0.0", request(), true);

        @SuppressWarnings("unchecked")
        ArgumentCaptor<List<RawFinding>> captor = ArgumentCaptor.forClass(List.class);
        verify(assembler).assemble(any(ExecutionContext.class), eq(version), captor.capture(),
                anyMap(), any(OffsetDateTime.class));
        assertThat(captor.getValue())
                .anySatisfy(f -> assertThat(f.getCode()).isEqualTo("check-execution-error"));
        // the failure finding still carries the originating check_id so it can be persisted
        assertThat(captor.getValue())
                .anySatisfy(f -> assertThat(f.getCheckId()).isEqualTo(c.getCheckId()));
    }

    @Test
    @DisplayName("the originating check_id is stamped onto findings the executor returns (real FK, not the local id)")
    void stampsRealCheckIdOntoFindings() throws Exception {
        RubricCheck c = check(true);
        when(resolver.resolve("piqi.core", "1.0.0", true)).thenReturn(version);
        when(checkRepository.findByRubricVersionIdOrderByOrdinalAsc(versionId)).thenReturn(List.of(c));
        CheckExecutor executor = mock(CheckExecutor.class);
        // executor returns a finding WITHOUT a checkId — the service must stamp it from the check
        when(executor.execute(eq(c), any(ExecutionContext.class)))
                .thenReturn(List.of(RawFinding.builder().checkLocalId("c1").dimension(PiqiDimension.CONFORMANCE)
                        .severity(Severity.ERROR).code("x").build()));
        when(registry.get(CheckType.FHIRPATH)).thenReturn(executor);
        stubAssembler();

        service.evaluate("piqi.core", "1.0.0", request(), true);

        @SuppressWarnings("unchecked")
        ArgumentCaptor<List<RawFinding>> captor = ArgumentCaptor.forClass(List.class);
        verify(assembler).assemble(any(ExecutionContext.class), eq(version), captor.capture(),
                anyMap(), any(OffsetDateTime.class));
        assertThat(captor.getValue()).allSatisfy(f -> assertThat(f.getCheckId()).isEqualTo(c.getCheckId()));
    }
}
