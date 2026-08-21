package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategorySeverity;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.EvaluateRequestDto;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.SubjectDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import com.lantanagroup.link.validation.services.categoryoverride.CategoryOverrideEngine;
import com.lantanagroup.link.validation.services.categoryoverride.CategorySequenceProvider;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import com.lantanagroup.link.validation.services.execution.CheckExecutorRegistry;
import com.lantanagroup.link.validation.services.scoring.FindingStatusResolver;
import com.lantanagroup.link.validation.services.scoring.ScoringPolicyResolver;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.doAnswer;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * End-to-end through the real assembler, aggregator, and override engine: only the category source
 * (CategorizationService) and the check executor are stubbed. Pins the whole chain the feature
 * depends on — findings flow through the engine, per-check statuses are derived post-override, and
 * the envelope carries both the effective severity and the diagnostics.
 */
class RubricExecutionServiceCategoryOverrideTest {

    private final RubricVersionResolver resolver = mock(RubricVersionResolver.class);
    private final CheckExecutorRegistry registry = mock(CheckExecutorRegistry.class);
    private final CategorizationService categorizationService = mock(CategorizationService.class);
    private final ObjectMapper objectMapper = new ObjectMapper();

    private final ValidationPolicyConfig policyConfig = new ValidationPolicyConfig();
    private final ScoreAggregator scoreAggregator = new ScoreAggregator(new FindingStatusResolver());
    private final ResultEnvelopeAssembler assembler = new ResultEnvelopeAssembler(
            objectMapper, scoreAggregator, new ScoringPolicyResolver(policyConfig, objectMapper), policyConfig);
    private final CategoryOverrideEngine overrideEngine = new CategoryOverrideEngine(
            categorizationService, new CategorySequenceProvider(objectMapper), policyConfig);

    private final RubricExecutionService service = new RubricExecutionService(
            resolver, mock(RubricVersionRepository.class), registry, assembler, overrideEngine,
            scoreAggregator, mock(RubricResultPersister.class), FhirContext.forR4(), objectMapper,
            Runnable::run, new com.lantanagroup.link.validation.services.execution.BundleReferenceResolver(), false);

    private final RubricVersion version = RubricVersion.builder()
            .rubricId("piqi.core").semver("1.0.0").rubricVersionId(UUID.randomUUID()).checksum("abc").build();

    private static Category acceptableWarning(String id) {
        Category category = new Category();
        category.setId(id);
        category.setTitle(id);
        category.setSeverity(CategorySeverity.WARNING);
        category.setAcceptable(true);
        category.setGuidance("guidance");
        return category;
    }

    private static RubricCheck conformanceCheck() {
        return RubricCheck.builder()
                .checkId(UUID.randomUUID())
                .checkLocalId("c1")
                .type(CheckType.FHIR_CONFORMANCE)
                .dimension(PiqiDimension.CONFORMANCE)
                .enabled(true)
                .build();
    }

    private EvaluateRequestDto request() throws Exception {
        JsonNode payload = objectMapper.readTree("{\"resourceType\":\"Patient\",\"id\":\"p1\"}");
        return EvaluateRequestDto.builder()
                .subject(SubjectDto.builder().facilityId("f1").build())
                .payload(payload)
                .build();
    }

    @Test
    @DisplayName("an error a category declares acceptable is downgraded end to end: status, score, and finding all reflect the override")
    void acceptableCategoryDowngradesAnErrorEndToEnd() throws Exception {
        policyConfig.getCategoryOverride().setEnabled(true);

        RubricCheck check = conformanceCheck();
        when(resolver.resolve("piqi.core", "1.0.0", false))
                .thenReturn(new RubricVersionResolver.ResolvedRubric(version, List.of(check)));
        CheckExecutor executor = mock(CheckExecutor.class);
        when(executor.execute(eq(check), any(ExecutionContext.class)))
                .thenReturn(List.of(RawFinding.builder()
                        .checkLocalId("c1").dimension(PiqiDimension.CONFORMANCE)
                        .severity(Severity.ERROR).code("fhir-conformance")
                        .message("some validator message").build()));
        when(registry.get(CheckType.FHIR_CONFORMANCE)).thenReturn(executor);
        doAnswer(invocation -> {
            List<Result> results = invocation.getArgument(0);
            results.forEach(r -> r.setCategories(List.of(acceptableWarning("cat-a"))));
            return null;
        }).when(categorizationService).categorize(anyList());

        ValidationResultEnvelope envelope = service.evaluate("piqi.core", "1.0.0", request(), false);

        assertThat(envelope.getStatus()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        FindingDto finding = envelope.getFindings().get(0);
        assertThat(finding.getSeverity()).isEqualTo(Severity.WARNING);
        assertThat(finding.getOriginalSeverity()).isEqualTo(Severity.ERROR);
        assertThat(finding.getOverriddenSeverity()).isEqualTo(Severity.WARNING);
        assertThat(finding.getAcceptable()).isTrue();
        assertThat(finding.getGoverningCategoryId()).isEqualTo("cat-a");
        // the summary still reconciles with what the check emitted
        assertThat(envelope.getSummary().getErrorCount()).isEqualTo(1);
        assertThat(envelope.getSummary().getWarningCount()).isZero();
    }

    @Test
    @DisplayName("the override engine covers every check type, not just FHIR conformance: a TERMINOLOGY error is downgraded too")
    void nonConformanceCheckIsAlsoInReach() throws Exception {
        policyConfig.getCategoryOverride().setEnabled(true);

        RubricCheck check = RubricCheck.builder()
                .checkId(UUID.randomUUID())
                .checkLocalId("c1")
                .type(CheckType.TERMINOLOGY)
                .dimension(PiqiDimension.TERMINOLOGY)
                .enabled(true)
                .build();
        when(resolver.resolve("piqi.core", "1.0.0", false))
                .thenReturn(new RubricVersionResolver.ResolvedRubric(version, List.of(check)));
        CheckExecutor executor = mock(CheckExecutor.class);
        when(executor.execute(eq(check), any(ExecutionContext.class)))
                .thenReturn(List.of(RawFinding.builder()
                        .checkLocalId("c1").dimension(PiqiDimension.TERMINOLOGY)
                        .severity(Severity.ERROR).code("terminology-code-invalid")
                        .message("bad code").build()));
        when(registry.get(CheckType.TERMINOLOGY)).thenReturn(executor);
        doAnswer(invocation -> {
            List<Result> results = invocation.getArgument(0);
            results.forEach(r -> r.setCategories(List.of(acceptableWarning("cat-a"))));
            return null;
        }).when(categorizationService).categorize(anyList());

        ValidationResultEnvelope envelope = service.evaluate("piqi.core", "1.0.0", request(), false);

        assertThat(envelope.getStatus()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        assertThat(envelope.getFindings().get(0).getAcceptable()).isTrue();
        assertThat(envelope.getFindings().get(0).getSeverity()).isEqualTo(Severity.WARNING);
    }
}
