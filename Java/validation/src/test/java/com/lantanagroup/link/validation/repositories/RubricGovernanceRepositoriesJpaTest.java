package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import com.lantanagroup.link.validation.entities.RubricResult;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.autoconfigure.orm.jpa.TestEntityManager;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * JPA-slice coverage for the rubric-governance repositories' custom finders and guarded bulk updates
 * against a real Hibernate session, matching the H2/MSSQLServer-compatibility-mode pattern used by
 * {@code CategoryInitializationJpaTest}. Mocked-repository unit tests elsewhere (e.g. RubricRegistryServiceTest)
 * cover the calling services but never execute these queries for real.
 */
@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:rubricgovernance;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import(RubricGovernanceRepositoriesJpaTest.Config.class)
class RubricGovernanceRepositoriesJpaTest {

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
    private RubricRepository rubricRepository;
    @Autowired
    private RubricVersionRepository versionRepository;
    @Autowired
    private RubricCheckRepository checkRepository;
    @Autowired
    private RubricLifecycleEventRepository lifecycleEventRepository;
    @Autowired
    private RubricResultRepository resultRepository;
    @Autowired
    private RubricFindingRepository findingRepository;

    private void persistRubric(String id) {
        entityManager.persistAndFlush(Rubric.builder().rubricId(id).title(id).owner("qa").build());
    }

    private RubricVersion persistVersion(String rubricId, String semver, RubricVersionStatus status) {
        RubricVersion version = RubricVersion.builder()
                .rubricId(rubricId).semver(semver).status(status).checksum("checksum-" + semver).build();
        return entityManager.persistAndFlush(version);
    }

    private RubricCheck persistCheck(Long rubricVersionId, String localId, int ordinal, boolean deleted) {
        RubricCheck check = RubricCheck.builder()
                .rubricVersionId(rubricVersionId)
                .checkLocalId(localId)
                .type(CheckType.CUSTOM)
                .dimension(PiqiDimension.CONFORMANCE)
                .ordinal(ordinal)
                .enabled(true)
                .deleted(deleted)
                .build();
        return entityManager.persistAndFlush(check);
    }

    private void persistEvent(String rubricId, RubricLifecycleAction action, OffsetDateTime occurredAt) {
        entityManager.persistAndFlush(RubricLifecycleEvent.builder()
                .rubricId(rubricId).semver("1.0.0").action(action).occurredAt(occurredAt).build());
    }

    // ------------------------------------------------------------------
    // RubricRepository
    // ------------------------------------------------------------------

    @Test
    void findByVersionStatus_returnsOnlyRubricsWithAMatchingVersionStatus() {
        persistRubric("piqi.core");
        persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistRubric("piqi.draftonly");
        persistVersion("piqi.draftonly", "1.0.0", RubricVersionStatus.DRAFT);
        entityManager.clear();

        Page<Rubric> page = rubricRepository.findByVersionStatus(RubricVersionStatus.PUBLISHED, PageRequest.of(0, 10));

        assertThat(page.getContent()).extracting(Rubric::getRubricId).containsExactly("piqi.core");
    }

    // ------------------------------------------------------------------
    // RubricVersionRepository
    // ------------------------------------------------------------------

    @Test
    void findByRubricId_returnsEveryVersionOfThatRubric() {
        persistRubric("piqi.core");
        persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistVersion("piqi.core", "2.0.0", RubricVersionStatus.DRAFT);
        entityManager.clear();

        assertThat(versionRepository.findByRubricId("piqi.core")).hasSize(2);
    }

    @Test
    void findByRubricIdAndStatus_filtersByStatus() {
        persistRubric("piqi.core");
        persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistVersion("piqi.core", "2.0.0", RubricVersionStatus.DRAFT);
        entityManager.clear();

        List<RubricVersion> published = versionRepository.findByRubricIdAndStatus("piqi.core", RubricVersionStatus.PUBLISHED);

        assertThat(published).extracting(RubricVersion::getSemver).containsExactly("1.0.0");
    }

    @Test
    void findByRubricIdAndSemver_returnsTheExactVersion() {
        persistRubric("piqi.core");
        persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        entityManager.clear();

        assertThat(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0")).isPresent();
        assertThat(versionRepository.findByRubricIdAndSemver("piqi.core", "9.9.9")).isEmpty();
    }

    @Test
    void findByRubricIdIn_batchLoadsVersionsForAPageOfRubrics() {
        persistRubric("piqi.core");
        persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistRubric("piqi.other");
        persistVersion("piqi.other", "1.0.0", RubricVersionStatus.DRAFT);
        entityManager.clear();

        List<RubricVersion> versions = versionRepository.findByRubricIdIn(List.of("piqi.core", "piqi.other"));

        assertThat(versions).extracting(RubricVersion::getRubricId)
                .containsExactlyInAnyOrder("piqi.core", "piqi.other");
    }

    @Test
    void findByRubricIdInAndStatus_combinesBothFilters() {
        persistRubric("piqi.core");
        persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistRubric("piqi.other");
        persistVersion("piqi.other", "1.0.0", RubricVersionStatus.DRAFT);
        entityManager.clear();

        List<RubricVersion> versions = versionRepository.findByRubricIdInAndStatus(
                List.of("piqi.core", "piqi.other"), RubricVersionStatus.PUBLISHED);

        assertThat(versions).extracting(RubricVersion::getRubricId).containsExactly("piqi.core");
    }

    @Test
    void recordDryRun_updatesOnlyTheDryRunColumns() {
        persistRubric("piqi.core");
        RubricVersion version = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.DRAFT);
        entityManager.clear();
        OffsetDateTime completedAt = OffsetDateTime.now();

        int updated = versionRepository.recordDryRun(
                version.getRubricVersionId(), RubricResultStatus.ACCEPTABLE, completedAt);
        entityManager.clear();

        assertThat(updated).isEqualTo(1);
        RubricVersion reloaded = versionRepository.findById(version.getRubricVersionId()).orElseThrow();
        assertThat(reloaded.getDryRunStatus()).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(reloaded.getStatus()).isEqualTo(RubricVersionStatus.DRAFT);
    }

    @Test
    void publishIfStatus_flipsDraftToPublishedExactlyOnce() {
        persistRubric("piqi.core");
        RubricVersion version = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.DRAFT);
        entityManager.clear();
        OffsetDateTime publishedAt = OffsetDateTime.now();

        int firstAttempt = versionRepository.publishIfStatus(version.getRubricVersionId(),
                RubricVersionStatus.DRAFT, RubricVersionStatus.PUBLISHED, publishedAt, "qa");
        int secondAttempt = versionRepository.publishIfStatus(version.getRubricVersionId(),
                RubricVersionStatus.DRAFT, RubricVersionStatus.PUBLISHED, publishedAt, "qa");

        assertThat(firstAttempt).isEqualTo(1);
        assertThat(secondAttempt).as("a second publish attempt must be a no-op once already published").isEqualTo(0);
        RubricVersion reloaded = versionRepository.findById(version.getRubricVersionId()).orElseThrow();
        assertThat(reloaded.getStatus()).isEqualTo(RubricVersionStatus.PUBLISHED);
        assertThat(reloaded.getPublishedBy()).isEqualTo("qa");
    }

    @Test
    void retireIfNotRetired_flipsPublishedToRetiredExactlyOnce() {
        persistRubric("piqi.core");
        RubricVersion version = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        entityManager.clear();
        OffsetDateTime retiredAt = OffsetDateTime.now();

        int firstAttempt = versionRepository.retireIfNotRetired(
                version.getRubricVersionId(), RubricVersionStatus.RETIRED, retiredAt, "qa");
        int secondAttempt = versionRepository.retireIfNotRetired(
                version.getRubricVersionId(), RubricVersionStatus.RETIRED, retiredAt, "qa");

        assertThat(firstAttempt).isEqualTo(1);
        assertThat(secondAttempt).as("a double retire must be a no-op").isEqualTo(0);
        RubricVersion reloaded = versionRepository.findById(version.getRubricVersionId()).orElseThrow();
        assertThat(reloaded.getStatus()).isEqualTo(RubricVersionStatus.RETIRED);
    }

    // ------------------------------------------------------------------
    // RubricCheckRepository
    // ------------------------------------------------------------------

    @Test
    void findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc_ordersByOrdinalAndHidesDeleted() {
        persistRubric("piqi.core");
        RubricVersion version = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.DRAFT);
        persistCheck(version.getRubricVersionId(), "c2", 2, false);
        persistCheck(version.getRubricVersionId(), "c1", 1, false);
        persistCheck(version.getRubricVersionId(), "c-deleted", 0, true);
        entityManager.clear();

        List<RubricCheck> checks =
                checkRepository.findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(version.getRubricVersionId());

        assertThat(checks).extracting(RubricCheck::getCheckLocalId).containsExactly("c1", "c2");
    }

    @Test
    void softDeleteByRubricVersionId_flagsEveryLiveCheckForThatVersion() {
        persistRubric("piqi.core");
        RubricVersion version = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.DRAFT);
        persistCheck(version.getRubricVersionId(), "c1", 0, false);
        persistCheck(version.getRubricVersionId(), "c2", 1, false);
        entityManager.clear();

        int updated = checkRepository.softDeleteByRubricVersionId(version.getRubricVersionId());
        entityManager.clear();

        assertThat(updated).isEqualTo(2);
        assertThat(checkRepository.findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(version.getRubricVersionId()))
                .isEmpty();
    }

    // ------------------------------------------------------------------
    // RubricLifecycleEventRepository
    // ------------------------------------------------------------------

    @Test
    void findByRubricIdOrderByOccurredAtDesc_returnsNewestFirst() {
        persistRubric("piqi.core");
        persistEvent("piqi.core", RubricLifecycleAction.REGISTERED, OffsetDateTime.now().minusHours(2));
        persistEvent("piqi.core", RubricLifecycleAction.PUBLISHED, OffsetDateTime.now());
        entityManager.clear();

        List<RubricLifecycleEvent> events = lifecycleEventRepository.findByRubricIdOrderByOccurredAtDesc("piqi.core");

        assertThat(events).extracting(RubricLifecycleEvent::getAction)
                .containsExactly(RubricLifecycleAction.PUBLISHED, RubricLifecycleAction.REGISTERED);
    }

    // ------------------------------------------------------------------
    // RubricResultRepository / RubricFindingRepository
    // ------------------------------------------------------------------

    @Test
    void rubricResultAndFinding_derivedFindersRoundTrip() {
        persistRubric("piqi.core");
        RubricVersion version = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        RubricCheck check = persistCheck(version.getRubricVersionId(), "c1", 0, false);
        UUID requestId = UUID.randomUUID();
        RubricResult result = RubricResult.builder()
                .requestId(requestId)
                .rubricId("piqi.core")
                .rubricVersionId(version.getRubricVersionId())
                .status(RubricResultStatus.ACCEPTABLE)
                .requestedAt(OffsetDateTime.now())
                .completedAt(OffsetDateTime.now())
                .durationMs(5)
                .build();
        result = entityManager.persistAndFlush(result);
        RubricFinding finding = RubricFinding.builder()
                .resultId(result.getResultId())
                .checkId(check.getCheckId())
                .dimension(PiqiDimension.CONFORMANCE)
                .severity(Severity.ERROR)
                .code("x")
                .message("m")
                .build();
        entityManager.persistAndFlush(finding);
        entityManager.clear();

        assertThat(resultRepository.findByRequestId(requestId)).isPresent();
        assertThat(findingRepository.findByResultId(result.getResultId())).hasSize(1);
    }
}
