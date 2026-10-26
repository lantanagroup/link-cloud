package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.LegacyShadowFinding;
import com.lantanagroup.link.validation.entities.LegacyShadowResult;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.autoconfigure.orm.jpa.TestEntityManager;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * JPA-slice coverage for the ADR-0003 shadow-validation repositories' custom finders, against a real
 * Hibernate session (H2 in MSSQLServer compatibility mode, matching {@code CategoryInitializationJpaTest}).
 */
@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:shadowvalidation;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import(ShadowRepositoriesJpaTest.Config.class)
class ShadowRepositoriesJpaTest {

    @TestConfiguration
    static class Config {
        /** Required by MatcherConverter, which Hibernate resolves from the application context
         * when it bootstraps the persistence unit (it scans every entity, not just this test's). */
        @Bean
        ObjectMapper objectMapper() {
            return new ObjectMapper();
        }
    }

    @Autowired
    private TestEntityManager entityManager;
    @Autowired
    private LegacyShadowResultRepository resultRepository;
    @Autowired
    private LegacyShadowFindingRepository findingRepository;
    @Autowired
    private ShadowComparisonResultRepository comparisonResultRepository;

    private LegacyShadowResult persistResult(UUID requestId, OffsetDateTime requestedAt) {
        LegacyShadowResult result = LegacyShadowResult.builder()
                .requestId(requestId)
                .facilityId("f1").patientId("p1").reportId("r1")
                .requestedAt(requestedAt).completedAt(requestedAt.plusSeconds(1)).durationMs(500)
                .build();
        return entityManager.persistAndFlush(result);
    }

    private ShadowComparisonResult persistComparison(UUID requestId, OffsetDateTime comparedAt) {
        ShadowComparisonResult result = ShadowComparisonResult.builder()
                .requestId(requestId)
                .facilityId("f1").patientId("p1").reportId("r1")
                .ranNewEngine(true).matched(true).comparedAt(comparedAt)
                .build();
        return entityManager.persistAndFlush(result);
    }

    @Test
    void legacyShadowResult_findFirstByRequestIdOrderByRequestedAtDesc_returnsTheLatest() {
        UUID requestId = UUID.randomUUID();
        persistResult(requestId, OffsetDateTime.now().minusHours(1));
        LegacyShadowResult latest = persistResult(requestId, OffsetDateTime.now());
        entityManager.clear();

        Optional<LegacyShadowResult> found = resultRepository.findFirstByRequestIdOrderByRequestedAtDesc(requestId);

        assertThat(found).isPresent();
        assertThat(found.get().getResultId()).isEqualTo(latest.getResultId());
    }

    @Test
    void legacyShadowFinding_findByRequestId_returnsFindingsForThatRequest() {
        UUID requestId = UUID.randomUUID();
        LegacyShadowResult result = persistResult(requestId, OffsetDateTime.now());
        LegacyShadowFinding finding = LegacyShadowFinding.builder()
                .resultId(result.getResultId())
                .requestId(requestId)
                .severity(OperationOutcome.IssueSeverity.WARNING)
                .code(OperationOutcome.IssueType.INVALID)
                .message("m")
                .build();
        entityManager.persistAndFlush(finding);
        entityManager.clear();

        List<LegacyShadowFinding> findings = findingRepository.findByRequestId(requestId);

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getMessage()).isEqualTo("m");
    }

    @Test
    void shadowComparisonResult_findByComparedAtBetween_filtersByRange() {
        OffsetDateTime now = OffsetDateTime.now();
        persistComparison(null, now.minusDays(2));
        ShadowComparisonResult inRange = persistComparison(null, now);
        entityManager.clear();

        List<ShadowComparisonResult> results =
                comparisonResultRepository.findByComparedAtBetween(now.minusHours(1), now.plusHours(1));

        assertThat(results).extracting(ShadowComparisonResult::getId).containsExactly(inRange.getId());
    }

    @Test
    void shadowComparisonResult_findByRequestIdOrderByComparedAtDesc_ordersNewestFirst() {
        UUID requestId = UUID.randomUUID();
        ShadowComparisonResult older = persistComparison(requestId, OffsetDateTime.now().minusHours(2));
        ShadowComparisonResult newer = persistComparison(requestId, OffsetDateTime.now());
        entityManager.clear();

        List<ShadowComparisonResult> results =
                comparisonResultRepository.findByRequestIdOrderByComparedAtDesc(requestId);

        assertThat(results).extracting(ShadowComparisonResult::getId)
                .containsExactly(newer.getId(), older.getId());
    }
}
