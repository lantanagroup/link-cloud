package com.lantanagroup.link.validation.repositories;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.records.RubricSearch;
import org.junit.jupiter.api.DisplayName;
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
import org.springframework.data.domain.Sort;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * The queries behind the rubric management interface, run for real: the list's search, the owners, check
 * counts, and the deletes behind deleting a draft. Same H2 setup as {@link RubricGovernanceRepositoriesJpaTest}.
 */
@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:rubricmanagement;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import(RubricManagementRepositoriesJpaTest.Config.class)
class RubricManagementRepositoriesJpaTest {

    @TestConfiguration
    static class Config {
        /** Hibernate resolves MatcherConverter from the context when it scans every entity. */
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

    private void persistRubric(String id, String title, String owner) {
        entityManager.persistAndFlush(Rubric.builder().rubricId(id).title(title).owner(owner).build());
    }

    private RubricVersion persistVersion(String rubricId, String semver, RubricVersionStatus status) {
        return entityManager.persistAndFlush(RubricVersion.builder()
                .rubricId(rubricId).semver(semver).status(status).checksum("checksum-" + semver).build());
    }

    private void persistCheck(Long rubricVersionId, String localId, boolean deleted) {
        entityManager.persistAndFlush(RubricCheck.builder()
                .rubricVersionId(rubricVersionId).checkLocalId(localId).type(CheckType.FHIRPATH)
                .dimension(PiqiDimension.CONFORMANCE).enabled(true).deleted(deleted).build());
    }

    private void persistEvent(String rubricId, RubricLifecycleAction action) {
        entityManager.persistAndFlush(RubricLifecycleEvent.builder().rubricId(rubricId).semver("1.0.0").action(action).build());
    }

    /** Three rubrics: one published with a draft, one only a draft, one only retired and without an owner. */
    private void persistThreeRubrics() {
        persistRubric("bare-minimum-requirements", "ACH Monthly Bare-Minimum Requirements", "lcg");
        persistVersion("bare-minimum-requirements", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistVersion("bare-minimum-requirements", "1.1.0", RubricVersionStatus.DRAFT);
        persistRubric("cross-measure-patient", "Cross-Measure Patient", "Lantana");
        persistVersion("cross-measure-patient", "0.3.0", RubricVersionStatus.DRAFT);
        persistRubric("old_100%", "Retired thing", null);
        persistVersion("old_100%", "1.0.0", RubricVersionStatus.RETIRED);
        entityManager.clear();
    }

    private List<String> search(RubricSearch search) {
        return rubricRepository.findAll(RubricSpecifications.matching(search), PageRequest.of(0, 10, Sort.by("rubricId")))
                .map(Rubric::getRubricId).getContent();
    }

    // ------------------------------------------------------------------
    // The list's search
    // ------------------------------------------------------------------

    @Test
    @DisplayName("no filters -> every rubric")
    void search_any() {
        persistThreeRubrics();

        assertThat(search(RubricSearch.ANY)).containsExactly("bare-minimum-requirements", "cross-measure-patient", "old_100%");
    }

    @Test
    @DisplayName("text is found in the id, the title or the owner, whatever its case")
    void search_text() {
        persistThreeRubrics();

        assertThat(search(new RubricSearch(null, "MINIMUM", null))).containsExactly("bare-minimum-requirements"); // id and title
        assertThat(search(new RubricSearch(null, "cross-measure pat", null))).containsExactly("cross-measure-patient"); // title only
        assertThat(search(new RubricSearch(null, "lantana", null))).containsExactly("cross-measure-patient"); // owner
        assertThat(search(new RubricSearch(null, "measure", null))).containsExactly("cross-measure-patient");
        assertThat(search(new RubricSearch(null, "nothing-like-this", null))).isEmpty();
    }

    @Test
    @DisplayName("% and _ in the text are characters to find, not wildcards")
    void search_textIsNotAPattern() {
        persistThreeRubrics();

        assertThat(search(new RubricSearch(null, "100%", null))).containsExactly("old_100%");
        assertThat(search(new RubricSearch(null, "d_1", null))).containsExactly("old_100%");
        // as wildcards these would match every rubric
        assertThat(search(new RubricSearch(null, "%", null))).containsExactly("old_100%");
        assertThat(search(new RubricSearch(null, "_", null))).containsExactly("old_100%");
    }

    @Test
    @DisplayName("owner is an exact match, and a rubric without an owner never matches one")
    void search_owner() {
        persistThreeRubrics();

        assertThat(search(new RubricSearch(null, null, "lcg"))).containsExactly("bare-minimum-requirements");
        assertThat(search(new RubricSearch(null, null, "lc"))).isEmpty();
    }

    @Test
    @DisplayName("status keeps the rubrics with a version in that status")
    void search_status() {
        persistThreeRubrics();

        assertThat(search(new RubricSearch(RubricVersionStatus.DRAFT, null, null)))
                .containsExactly("bare-minimum-requirements", "cross-measure-patient");
        assertThat(search(new RubricSearch(RubricVersionStatus.RETIRED, null, null))).containsExactly("old_100%");
    }

    @Test
    @DisplayName("the filters combine: all of them must hold")
    void search_combined() {
        persistThreeRubrics();

        assertThat(search(new RubricSearch(RubricVersionStatus.DRAFT, "measure", "Lantana"))).containsExactly("cross-measure-patient");
        assertThat(search(new RubricSearch(RubricVersionStatus.PUBLISHED, "measure", null))).isEmpty();
    }

    @Test
    @DisplayName("a rubric with several versions in the status is returned once, and the total counts rubrics")
    void search_countsRubricsNotVersions() {
        persistRubric("piqi.core", "PIQI Core", "qa");
        persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistVersion("piqi.core", "1.1.0", RubricVersionStatus.PUBLISHED);
        persistVersion("piqi.core", "1.2.0", RubricVersionStatus.PUBLISHED);
        entityManager.clear();

        Page<Rubric> page = rubricRepository.findAll(
                RubricSpecifications.matching(new RubricSearch(RubricVersionStatus.PUBLISHED, null, null)), PageRequest.of(0, 10));

        assertThat(page.getContent()).hasSize(1);
        assertThat(page.getTotalElements()).isEqualTo(1);
    }

    @Test
    @DisplayName("the search pages and sorts")
    void search_pagesAndSorts() {
        persistThreeRubrics();

        Page<Rubric> second = rubricRepository.findAll(RubricSpecifications.matching(RubricSearch.ANY),
                PageRequest.of(1, 2, Sort.by(Sort.Order.desc("title"), Sort.Order.asc("rubricId"))));

        assertThat(second.getTotalElements()).isEqualTo(3);
        assertThat(second.getTotalPages()).isEqualTo(2);
        // titles descending: Retired thing, Cross-Measure Patient, ACH Monthly...
        assertThat(second.getContent()).extracting(Rubric::getRubricId).containsExactly("bare-minimum-requirements");
    }

    @Test
    @DisplayName("owners are distinct, in order, without the rubrics that have none")
    void findDistinctOwners() {
        persistThreeRubrics();
        persistRubric("another", "Another", "lcg");
        persistRubric("blank-owner", "Blank owner", "");
        entityManager.clear();

        assertThat(rubricRepository.findDistinctOwners()).containsExactly("Lantana", "lcg");
    }

    // ------------------------------------------------------------------
    // Check counts
    // ------------------------------------------------------------------

    @Test
    @DisplayName("counts a version's live checks; replaced checks and versions that were not asked about are left out")
    void countLiveByRubricVersionIdIn() {
        persistRubric("piqi.core", "PIQI Core", "qa");
        RubricVersion first = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        RubricVersion second = persistVersion("piqi.core", "1.1.0", RubricVersionStatus.DRAFT);
        RubricVersion empty = persistVersion("piqi.core", "1.2.0", RubricVersionStatus.DRAFT);
        RubricVersion notAsked = persistVersion("piqi.core", "2.0.0", RubricVersionStatus.DRAFT);
        persistCheck(first.getRubricVersionId(), "a", false);
        persistCheck(first.getRubricVersionId(), "b", false);
        persistCheck(first.getRubricVersionId(), "replaced", true);
        persistCheck(second.getRubricVersionId(), "a", false);
        persistCheck(notAsked.getRubricVersionId(), "a", false);
        entityManager.clear();

        Map<Long, Long> counts = new HashMap<>();
        for (Object[] row : checkRepository.countLiveByRubricVersionIdIn(
                List.of(first.getRubricVersionId(), second.getRubricVersionId(), empty.getRubricVersionId()))) {
            counts.put((Long) row[0], ((Number) row[1]).longValue());
        }

        assertThat(counts).containsOnly(
                Map.entry(first.getRubricVersionId(), 2L),
                Map.entry(second.getRubricVersionId(), 1L));
    }

    // ------------------------------------------------------------------
    // Deleting a draft
    // ------------------------------------------------------------------

    @Test
    @DisplayName("deleteByRubricVersionId removes a version's checks, the replaced ones too, and no other version's")
    void deleteByRubricVersionId() {
        persistRubric("piqi.core", "PIQI Core", "qa");
        RubricVersion draft = persistVersion("piqi.core", "1.1.0", RubricVersionStatus.DRAFT);
        RubricVersion other = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
        persistCheck(draft.getRubricVersionId(), "a", false);
        persistCheck(draft.getRubricVersionId(), "replaced", true);
        persistCheck(other.getRubricVersionId(), "a", false);

        int deleted = checkRepository.deleteByRubricVersionId(draft.getRubricVersionId());

        assertThat(deleted).isEqualTo(2);
        assertThat(checkRepository.findAll()).extracting(RubricCheck::getRubricVersionId)
                .containsExactly(other.getRubricVersionId());
    }

    @Test
    @DisplayName("deleteIfStatus deletes a version in the expected status")
    void deleteIfStatus_deletes() {
        persistRubric("piqi.core", "PIQI Core", "qa");
        RubricVersion draft = persistVersion("piqi.core", "1.1.0", RubricVersionStatus.DRAFT);

        assertThat(versionRepository.deleteIfStatus(draft.getRubricVersionId(), RubricVersionStatus.DRAFT)).isEqualTo(1);
        assertThat(versionRepository.findByRubricIdAndSemver("piqi.core", "1.1.0")).isEmpty();
        assertThat(versionRepository.countByRubricId("piqi.core")).isZero();
    }

    @Test
    @DisplayName("deleteIfStatus leaves a version in another status alone")
    void deleteIfStatus_guards() {
        persistRubric("piqi.core", "PIQI Core", "qa");
        RubricVersion published = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);

        assertThat(versionRepository.deleteIfStatus(published.getRubricVersionId(), RubricVersionStatus.DRAFT)).isZero();
        assertThat(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0")).isPresent();
        assertThat(versionRepository.countByRubricId("piqi.core")).isEqualTo(1);
    }

    @Test
    @DisplayName("a draft with checks, and then its rubric with its history, can be deleted in that order")
    void deletingTheLastDraftAndItsRubric() {
        persistRubric("piqi.core", "PIQI Core", "qa");
        persistRubric("other", "Other", "qa");
        RubricVersion draft = persistVersion("piqi.core", "1.0.0", RubricVersionStatus.DRAFT);
        persistCheck(draft.getRubricVersionId(), "a", false);
        persistEvent("piqi.core", RubricLifecycleAction.REGISTERED);
        persistEvent("other", RubricLifecycleAction.REGISTERED);

        // the order RubricRegistryService.deleteDraft uses; any other violates a foreign key
        checkRepository.deleteByRubricVersionId(draft.getRubricVersionId());
        versionRepository.deleteIfStatus(draft.getRubricVersionId(), RubricVersionStatus.DRAFT);
        assertThat(lifecycleEventRepository.deleteByRubricId("piqi.core")).isEqualTo(1);
        rubricRepository.deleteById("piqi.core");
        rubricRepository.flush();
        entityManager.clear();

        assertThat(rubricRepository.findById("piqi.core")).isEmpty();
        assertThat(rubricRepository.findById("other")).isPresent();
        assertThat(lifecycleEventRepository.findByRubricIdOrderByOccurredAtDesc("other")).hasSize(1);
    }

    @Test
    @DisplayName("a DELETED event can be stored and read back")
    void deletedEventRoundTrips() {
        persistRubric("piqi.core", "PIQI Core", "qa");
        persistEvent("piqi.core", RubricLifecycleAction.DELETED);
        entityManager.clear();

        assertThat(lifecycleEventRepository.findByRubricIdOrderByOccurredAtDesc("piqi.core"))
                .extracting(RubricLifecycleEvent::getAction).containsExactly(RubricLifecycleAction.DELETED);
    }
}
