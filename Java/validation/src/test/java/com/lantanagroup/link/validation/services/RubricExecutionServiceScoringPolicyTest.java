package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.models.EvaluateRequestDto;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RubricVersionSnapshot;
import com.lantanagroup.link.validation.models.SubjectDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import com.lantanagroup.link.validation.services.execution.CheckExecutorRegistry;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Regression test for the scoring↔cache interaction: the resolver hands the evaluate path a
 * version rebuilt from {@link RubricVersionSnapshot} (the cached form), not the JPA entity.
 * If the snapshot drops scoringPolicyJson, the real assembler silently scores every rubric
 * with the default dimension/worst-of policy — so this test wires the REAL assembler and
 * aggregator and stubs the resolver exactly the way production builds it.
 */
class RubricExecutionServiceScoringPolicyTest {

    private final RubricVersionResolver resolver = mock(RubricVersionResolver.class);
    private final RubricVersionRepository versionRepository = mock(RubricVersionRepository.class);
    private final CheckExecutorRegistry registry = mock(CheckExecutorRegistry.class);
    private final RubricResultPersister resultPersister = mock(RubricResultPersister.class);
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final ScoreAggregator scoreAggregator = new ScoreAggregator();
    private final ResultEnvelopeAssembler assembler = new ResultEnvelopeAssembler(objectMapper, scoreAggregator);

    private final RubricExecutionService service = new RubricExecutionService(
            resolver, versionRepository, registry, assembler,
            scoreAggregator, resultPersister, FhirContext.forR4(), objectMapper,
            Runnable::run, false);

    @Test
    @DisplayName("a non-default scoring policy survives snapshot resolution: check-scorecard rubrics score byCheck, not the dimension default")
    void scoringPolicySurvivesSnapshotResolution() throws Exception {
        RubricVersion entity = RubricVersion.builder()
                .rubricVersionId(UUID.randomUUID())
                .rubricId("piqi.core")
                .semver("1.0.0")
                .status(RubricVersionStatus.PUBLISHED)
                .checksum("abc")
                .scoringPolicyJson("{\"type\":\"piqi-check-scorecard\",\"rollup\":\"worst-of\"}")
                .build();
        RubricCheck check = RubricCheck.builder()
                .checkId(UUID.randomUUID())
                .rubricVersionId(entity.getRubricVersionId())
                .checkLocalId("c1")
                .type(CheckType.FHIRPATH)
                .dimension(PiqiDimension.CONFORMANCE)
                .parametersJson("{\"expression\":\"Patient.name.exists()\"}")
                .ordinal(0)
                .enabled(true)
                .build();

        // resolve() returns snapshot-rebuilt entities in production — mirror that here
        RubricVersionSnapshot snapshot = RubricVersionSnapshot.from(entity, List.of(check));
        when(resolver.resolve("piqi.core", "1.0.0", true))
                .thenReturn(new RubricVersionResolver.ResolvedRubric(
                        snapshot.toVersionEntity(), snapshot.toCheckEntities()));

        CheckExecutor executor = mock(CheckExecutor.class);
        when(executor.execute(any(RubricCheck.class), any(ExecutionContext.class))).thenReturn(List.of());
        when(registry.get(eq(CheckType.FHIRPATH))).thenReturn(executor);

        JsonNode payload = objectMapper.readTree("{\"resourceType\":\"Patient\",\"id\":\"p1\"}");
        EvaluateRequestDto request = EvaluateRequestDto.builder()
                .subject(SubjectDto.builder().facilityId("f1").build())
                .payload(payload)
                .build();

        ValidationResultEnvelope envelope = service.evaluate("piqi.core", "1.0.0", request, true);

        // check-scorecard policy → per-check scores; the pre-fix behavior fell back to the
        // default dimension scorecard (byCheck null, byDimension populated)
        assertThat(envelope.getScore()).isNotNull();
        assertThat(envelope.getScore().getByCheck())
                .as("scoring policy was dropped by the snapshot — scored with the default dimension policy")
                .isNotNull()
                .containsEntry("c1", RubricResultStatus.ACCEPTABLE);
        assertThat(envelope.getScore().getByDimension()).isNull();
        assertThat(envelope.getStatus()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }
}
