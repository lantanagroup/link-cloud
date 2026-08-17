package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.RubricDryRunConfig;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricDryRunRequiredException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.providers.RubricCacheService;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricLifecycleEventRepository;
import com.lantanagroup.link.validation.repositories.RubricRepository;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;
import org.springframework.dao.DataIntegrityViolationException;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class RubricRegistryServiceTest {

    private final RubricRepository rubricRepository = mock(RubricRepository.class);
    private final RubricVersionRepository versionRepository = mock(RubricVersionRepository.class);
    private final RubricCheckRepository checkRepository = mock(RubricCheckRepository.class);
    private final RubricLifecycleEventRepository eventRepository = mock(RubricLifecycleEventRepository.class);
    private final RubricDefinitionValidator definitionValidator = mock(RubricDefinitionValidator.class);
    private final RubricDryRunConfig dryRunConfig = new RubricDryRunConfig();
    private final RubricCacheService cacheService = mock(RubricCacheService.class);

    private RubricRegistryService service() {
        return new RubricRegistryService(
                rubricRepository, versionRepository, checkRepository, eventRepository,
                definitionValidator, new ObjectMapper(), dryRunConfig, cacheService);
    }

    private RubricVersion draftVersion() {
        return RubricVersion.builder()
                .rubricVersionId(UUID.randomUUID())
                .rubricId("piqi.core")
                .semver("1.0.0")
                .status(RubricVersionStatus.DRAFT)
                .checksum("abc123")
                .build();
    }

    private void stubVersion(RubricVersion version) {
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(version));
        when(versionRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
        // atomic guarded transitions win by default (1 row affected)
        when(versionRepository.publishIfStatus(any(), any(), any(), any(), any())).thenReturn(1);
        when(versionRepository.retireIfNotRetired(any(), any(), any(), any())).thenReturn(1);
    }

    @Test
    @DisplayName("losing a concurrent registration race -> 409 conflict, not a raw integrity violation")
    void concurrentRegistrationTranslatesToConflict() {
        // pre-check sees no existing version, but the insert loses the race on uq_rv_rubric_semver
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0")).thenReturn(Optional.empty());
        when(rubricRepository.findById("piqi.core")).thenReturn(Optional.empty());
        when(rubricRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
        when(versionRepository.saveAndFlush(any()))
                .thenThrow(new DataIntegrityViolationException("uq_rv_rubric_semver violated"));

        RubricVersionPayloadDto payload = RubricVersionPayloadDto.builder()
                .id("piqi.core")
                .semver("1.0.0")
                .build();

        assertThatThrownBy(() -> service().registerVersion(payload, "qa"))
                .isInstanceOf(RubricVersionConflictException.class);
    }

    private RubricVersionPayloadDto payloadWithCheck(String checkLocalId) {
        return RubricVersionPayloadDto.builder()
                .id("piqi.core")
                .semver("1.0.0")
                .title("PIQI Core")
                .checks(List.of(CheckDto.builder()
                        .id(checkLocalId)
                        .type(CheckType.FHIRPATH)
                        .dimension(PiqiDimension.CONFORMANCE)
                        .ordinal(1)
                        .enabled(true)
                        .build()))
                .build();
    }

    private void stubRubricUpsert() {
        when(rubricRepository.findById("piqi.core"))
                .thenReturn(Optional.of(Rubric.builder().rubricId("piqi.core").title("PIQI Core").build()));
        when(rubricRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
        when(versionRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
    }

    // mirrors the service's definition checksum (SHA-256 over the payload's JSON serialization)
    private static String checksumOf(RubricVersionPayloadDto payload) throws Exception {
        byte[] json = new ObjectMapper().writeValueAsString(payload)
                .getBytes(java.nio.charset.StandardCharsets.UTF_8);
        return java.util.HexFormat.of().formatHex(
                java.security.MessageDigest.getInstance("SHA-256").digest(json));
    }

    @Test
    @DisplayName("re-registering a DRAFT semver replaces the definition and checks in place")
    void reRegisterDraftReplacesDefinitionAndChecks() {
        RubricVersion draft = draftVersion();
        UUID originalId = draft.getRubricVersionId();
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(draft));
        stubRubricUpsert();

        RubricVersion result = service().registerVersion(payloadWithCheck("c-new"), "author2");

        // same row updated, no new version inserted
        assertThat(result.getRubricVersionId()).isEqualTo(originalId);
        assertThat(result.getStatus()).isEqualTo(RubricVersionStatus.DRAFT);
        assertThat(result.getDefinitionJson()).contains("c-new");
        assertThat(result.getChecksum()).isNotEqualTo("abc123");
        assertThat(result.getCreatedBy()).isEqualTo("author2");
        verify(versionRepository, never()).saveAndFlush(any());

        // old checks removed, replacement checks inserted
        verify(checkRepository).softDeleteByRubricVersionId(originalId);
        org.mockito.ArgumentCaptor<List<RubricCheck>> checksCaptor =
                org.mockito.ArgumentCaptor.forClass(List.class);
        verify(checkRepository).saveAll(checksCaptor.capture());
        assertThat(checksCaptor.getValue())
                .singleElement()
                .satisfies(c -> {
                    assertThat(c.getCheckLocalId()).isEqualTo("c-new");
                    assertThat(c.getRubricVersionId()).isEqualTo(originalId);
                    assertThat(c.isDeleted()).isFalse();
                });
    }

    @Test
    @DisplayName("re-registering a DRAFT records a REGISTERED lifecycle event again, with the new checksum")
    void reRegisterDraftRecordsLifecycleEvent() throws Exception {
        RubricVersion draft = draftVersion();
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(draft));
        stubRubricUpsert();
        RubricVersionPayloadDto payload = payloadWithCheck("c-new");

        service().registerVersion(payload, "qa");

        org.mockito.ArgumentCaptor<com.lantanagroup.link.validation.entities.RubricLifecycleEvent> eventCaptor =
                org.mockito.ArgumentCaptor.forClass(com.lantanagroup.link.validation.entities.RubricLifecycleEvent.class);
        verify(eventRepository).save(eventCaptor.capture());
        assertThat(eventCaptor.getValue().getAction())
                .isEqualTo(com.lantanagroup.link.validation.enums.RubricLifecycleAction.REGISTERED);
        assertThat(eventCaptor.getValue().getSemver()).isEqualTo("1.0.0");
        assertThat(eventCaptor.getValue().getChecksum()).isEqualTo(checksumOf(payload));
    }

    @Test
    @DisplayName("re-registering a DRAFT with a changed definition clears the dry-run gate")
    void reRegisterDraftWithChangedDefinitionClearsDryRun() {
        RubricVersion draft = draftVersion();
        draft.setDryRunCompletedAt(OffsetDateTime.now());
        draft.setDryRunStatus(RubricResultStatus.ACCEPTABLE);
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(draft));
        stubRubricUpsert();

        RubricVersion result = service().registerVersion(payloadWithCheck("c-new"), "qa");

        assertThat(result.getDryRunCompletedAt()).isNull();
        assertThat(result.getDryRunStatus()).isNull();
    }

    @Test
    @DisplayName("re-registering a DRAFT with an identical definition keeps the dry-run gate")
    void reRegisterDraftWithIdenticalDefinitionKeepsDryRun() throws Exception {
        RubricVersionPayloadDto payload = payloadWithCheck("c1");
        RubricVersion draft = draftVersion();
        draft.setChecksum(checksumOf(payload));
        OffsetDateTime dryRunAt = OffsetDateTime.now();
        draft.setDryRunCompletedAt(dryRunAt);
        draft.setDryRunStatus(RubricResultStatus.ACCEPTABLE);
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(draft));
        stubRubricUpsert();

        RubricVersion result = service().registerVersion(payload, "qa");

        assertThat(result.getDryRunCompletedAt()).isEqualTo(dryRunAt);
        assertThat(result.getDryRunStatus()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @ParameterizedTest
    @EnumSource(value = RubricVersionStatus.class, names = {"PUBLISHED", "RETIRED"})
    @DisplayName("re-registering a PUBLISHED or RETIRED semver -> 409 conflict, nothing is touched")
    void reRegisterNonDraftIsRejected(RubricVersionStatus status) {
        RubricVersion version = draftVersion();
        version.setStatus(status);
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(version));

        assertThatThrownBy(() -> service().registerVersion(payloadWithCheck("c-new"), "qa"))
                .isInstanceOf(RubricVersionConflictException.class)
                .hasMessageContaining(status.name());

        verify(versionRepository, never()).save(any());
        verify(checkRepository, never()).softDeleteByRubricVersionId(any());
        verify(checkRepository, never()).saveAll(anyList());
        verify(eventRepository, never()).save(any());
    }

    @Test
    @DisplayName("a first-time registration never deletes checks")
    void firstRegistrationDoesNotDeleteChecks() {
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0")).thenReturn(Optional.empty());
        when(rubricRepository.findById("piqi.core")).thenReturn(Optional.empty());
        when(rubricRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
        when(versionRepository.saveAndFlush(any())).thenAnswer(inv -> inv.getArgument(0));

        service().registerVersion(payloadWithCheck("c1"), "qa");

        verify(checkRepository, never()).softDeleteByRubricVersionId(any());
        verify(checkRepository).saveAll(anyList());
        verify(eventRepository).save(any());
    }

    @Test
    @DisplayName("dry-run gate off -> publish succeeds without any dry-run data")
    void publish_dryRunNotRequired() {
        stubVersion(draftVersion());

        RubricVersion published = service().publish("piqi.core", "1.0.0", "qa");

        assertThat(published.getStatus()).isEqualTo(RubricVersionStatus.PUBLISHED);
        verify(eventRepository).save(any());
    }

    @Test
    @DisplayName("dry-run gate on + no completed dry run -> publish blocked")
    void publish_dryRunRequiredButNotCompleted() {
        dryRunConfig.setRequiredForPublish(true);
        stubVersion(draftVersion());

        assertThatThrownBy(() -> service().publish("piqi.core", "1.0.0", "qa"))
                .isInstanceOf(RubricDryRunRequiredException.class)
                .hasMessageContaining("no dry run has been completed");
    }

    @ParameterizedTest
    @EnumSource(value = RubricResultStatus.class, names = {"UNACCEPTABLE", "INCONCLUSIVE"})
    @DisplayName("dry-run gate on + disqualifying status -> publish blocked")
    void publish_dryRunWithDisqualifyingStatus(RubricResultStatus status) {
        dryRunConfig.setRequiredForPublish(true);
        RubricVersion version = draftVersion();
        version.setDryRunCompletedAt(OffsetDateTime.now());
        version.setDryRunStatus(status);
        stubVersion(version);

        assertThatThrownBy(() -> service().publish("piqi.core", "1.0.0", "qa"))
                .isInstanceOf(RubricDryRunRequiredException.class)
                .hasMessageContaining("dry run status is " + status);
    }

    @ParameterizedTest
    @EnumSource(value = RubricResultStatus.class, names = {"ACCEPTABLE", "ACCEPTABLE_WITH_WARNINGS"})
    @DisplayName("dry-run gate on + acceptable status -> publish succeeds")
    void publish_dryRunAcceptable(RubricResultStatus status) {
        dryRunConfig.setRequiredForPublish(true);
        RubricVersion version = draftVersion();
        version.setDryRunCompletedAt(OffsetDateTime.now());
        version.setDryRunStatus(status);
        stubVersion(version);

        RubricVersion published = service().publish("piqi.core", "1.0.0", "qa");

        assertThat(published.getStatus()).isEqualTo(RubricVersionStatus.PUBLISHED);
        verify(eventRepository).save(any());
    }

    @Test
    @DisplayName("dry-run gate on + already published -> lifecycle error, not the dry-run error")
    void publish_lifecycleCheckedBeforeDryRun() {
        dryRunConfig.setRequiredForPublish(true);
        RubricVersion version = draftVersion();
        version.setStatus(RubricVersionStatus.PUBLISHED);
        stubVersion(version);

        assertThatThrownBy(() -> service().publish("piqi.core", "1.0.0", "qa"))
                .isInstanceOf(RubricLifecycleException.class);
    }

    @Test
    @DisplayName("publish that loses the atomic transition (0 rows) -> 409, no duplicate event, no evict")
    void publish_losesAtomicRace() {
        // read sees DRAFT and passes the guard, but the guarded UPDATE affects 0 rows because a
        // concurrent publish already flipped the row to PUBLISHED — the loser must 409, not double-publish
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(draftVersion()))
                .thenReturn(Optional.of(versionOf("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED)));
        when(versionRepository.publishIfStatus(any(), any(), any(), any(), any())).thenReturn(0);

        assertThatThrownBy(() -> service().publish("piqi.core", "1.0.0", "qa"))
                .isInstanceOf(RubricLifecycleException.class);
        verify(eventRepository, never()).save(any());
        verify(cacheService, never()).evictVersion(any(), any());
    }

    @Test
    @DisplayName("retire that loses the atomic transition (0 rows) -> 409, no duplicate event, no evict")
    void retire_losesAtomicRace() {
        RubricVersion published = draftVersion();
        published.setStatus(RubricVersionStatus.PUBLISHED);
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(published))
                .thenReturn(Optional.of(versionOf("piqi.core", "1.0.0", RubricVersionStatus.RETIRED)));
        when(versionRepository.retireIfNotRetired(any(), any(), any(), any())).thenReturn(0);

        assertThatThrownBy(() -> service().retire("piqi.core", "1.0.0", "qa"))
                .isInstanceOf(RubricLifecycleException.class);
        verify(eventRepository, never()).save(any());
        verify(cacheService, never()).evictVersion(any(), any());
    }

    @Test
    @DisplayName("publish performs the transition atomically (guarded UPDATE), not a read-modify-save")
    void publish_usesAtomicGuardedUpdate() {
        stubVersion(draftVersion());

        service().publish("piqi.core", "1.0.0", "qa");

        verify(versionRepository).publishIfStatus(any(),
                org.mockito.ArgumentMatchers.eq(RubricVersionStatus.DRAFT),
                org.mockito.ArgumentMatchers.eq(RubricVersionStatus.PUBLISHED), any(), org.mockito.ArgumentMatchers.eq("qa"));
        verify(versionRepository, never()).save(any());
    }

    @Test
    @DisplayName("retire a DRAFT -> allowed, version goes straight to RETIRED")
    void retire_draftAllowed() {
        stubVersion(draftVersion());

        RubricVersion retired = service().retire("piqi.core", "1.0.0", "qa");

        assertThat(retired.getStatus()).isEqualTo(RubricVersionStatus.RETIRED);
        assertThat(retired.getRetiredAt()).isNotNull();
        assertThat(retired.getRetiredBy()).isEqualTo("qa");
        verify(eventRepository).save(any());
    }

    @Test
    @DisplayName("retire removes the version's cache entry (and the latest pointer)")
    void retire_evictsCache() {
        RubricVersion version = draftVersion();
        version.setStatus(RubricVersionStatus.PUBLISHED);
        stubVersion(version);

        service().retire("piqi.core", "1.0.0", "qa");

        verify(cacheService).evictVersion("piqi.core", "1.0.0");
    }

    @Test
    @DisplayName("a failed retire (already RETIRED) does not touch the cache")
    void retire_rejectedDoesNotEvict() {
        RubricVersion version = draftVersion();
        version.setStatus(RubricVersionStatus.RETIRED);
        stubVersion(version);

        assertThatThrownBy(() -> service().retire("piqi.core", "1.0.0", "qa"))
                .isInstanceOf(RubricLifecycleException.class);
        verify(cacheService, never()).evictVersion(any(), any());
    }

    @Test
    @DisplayName("publish evicts the stale DRAFT snapshot and the latest pointer")
    void publish_evictsCache() {
        stubVersion(draftVersion());

        service().publish("piqi.core", "1.0.0", "qa");

        verify(cacheService).evictVersion("piqi.core", "1.0.0");
    }

    @Test
    @DisplayName("re-registering a DRAFT evicts its stale cached snapshot")
    void reRegisterDraft_evictsCache() {
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(draftVersion()));
        stubRubricUpsert();

        service().registerVersion(payloadWithCheck("c-new"), "qa");

        verify(cacheService).evictVersion("piqi.core", "1.0.0");
    }

    @Test
    @DisplayName("retire a PUBLISHED version -> RETIRED")
    void retire_publishedAllowed() {
        RubricVersion version = draftVersion();
        version.setStatus(RubricVersionStatus.PUBLISHED);
        stubVersion(version);

        RubricVersion retired = service().retire("piqi.core", "1.0.0", "qa");

        assertThat(retired.getStatus()).isEqualTo(RubricVersionStatus.RETIRED);
    }

    @Test
    @DisplayName("retire an already RETIRED version -> 409 lifecycle error")
    void retire_alreadyRetiredRejected() {
        RubricVersion version = draftVersion();
        version.setStatus(RubricVersionStatus.RETIRED);
        stubVersion(version);

        assertThatThrownBy(() -> service().retire("piqi.core", "1.0.0", "qa"))
                .isInstanceOf(RubricLifecycleException.class);
        verify(eventRepository, never()).save(any());
    }

    private RubricVersion versionOf(String rubricId, String semver, RubricVersionStatus status) {
        return RubricVersion.builder()
                .rubricVersionId(UUID.randomUUID())
                .rubricId(rubricId)
                .semver(semver)
                .status(status)
                .checksum("x")
                .build();
    }

    @Test
    @DisplayName("listRubrics without a status filter -> plain findAll")
    void listRubrics_noFilter() {
        org.springframework.data.domain.Pageable pageable = org.springframework.data.domain.Pageable.ofSize(20);
        org.springframework.data.domain.Page<Rubric> page =
                new org.springframework.data.domain.PageImpl<>(List.of(Rubric.builder().rubricId("piqi.core").title("t").build()));
        when(rubricRepository.findAll(pageable)).thenReturn(page);

        assertThat(service().listRubrics(null, pageable)).isSameAs(page);
        verify(rubricRepository, never()).findByVersionStatus(any(), any());
    }

    @Test
    @DisplayName("listRubrics with a status filter -> status-scoped query")
    void listRubrics_withFilter() {
        org.springframework.data.domain.Pageable pageable = org.springframework.data.domain.Pageable.ofSize(20);
        org.springframework.data.domain.Page<Rubric> page =
                new org.springframework.data.domain.PageImpl<>(List.of(Rubric.builder().rubricId("piqi.core").title("t").build()));
        when(rubricRepository.findByVersionStatus(RubricVersionStatus.PUBLISHED, pageable)).thenReturn(page);

        assertThat(service().listRubrics(RubricVersionStatus.PUBLISHED, pageable)).isSameAs(page);
        verify(rubricRepository, never()).findAll(pageable);
    }

    @Test
    @DisplayName("versionsByRubricId without a filter -> all versions, grouped and newest-first")
    void versionsByRubricId_noFilter() {
        when(versionRepository.findByRubricIdIn(List.of("piqi.core"))).thenReturn(List.of(
                versionOf("piqi.core", "1.0.0", RubricVersionStatus.RETIRED),
                versionOf("piqi.core", "1.10.0", RubricVersionStatus.PUBLISHED),
                versionOf("piqi.core", "1.2.0", RubricVersionStatus.PUBLISHED)));

        var grouped = service().versionsByRubricId(List.of("piqi.core"), null);

        // semantic semver ordering: 1.10.0 is newer than 1.2.0
        assertThat(grouped.get("piqi.core")).extracting(RubricVersion::getSemver)
                .containsExactly("1.10.0", "1.2.0", "1.0.0");
        verify(versionRepository, never()).findByRubricIdInAndStatus(any(), any());
    }

    @Test
    @DisplayName("versionsByRubricId with a status filter -> only that status is loaded")
    void versionsByRubricId_withFilter() {
        when(versionRepository.findByRubricIdInAndStatus(List.of("piqi.core"), RubricVersionStatus.PUBLISHED))
                .thenReturn(List.of(versionOf("piqi.core", "1.2.0", RubricVersionStatus.PUBLISHED)));

        var grouped = service().versionsByRubricId(List.of("piqi.core"), RubricVersionStatus.PUBLISHED);

        assertThat(grouped.get("piqi.core")).hasSize(1);
        assertThat(grouped.get("piqi.core").get(0).getStatus()).isEqualTo(RubricVersionStatus.PUBLISHED);
        verify(versionRepository, never()).findByRubricIdIn(any());
    }

    @Test
    @DisplayName("versionsByRubricId with no rubric ids -> empty map, no query at all")
    void versionsByRubricId_emptyIds() {
        assertThat(service().versionsByRubricId(List.of(), RubricVersionStatus.PUBLISHED)).isEmpty();
        verify(versionRepository, never()).findByRubricIdIn(any());
        verify(versionRepository, never()).findByRubricIdInAndStatus(any(), any());
    }

    @Test
    @DisplayName("register canonicalizes a leading-zero semver before the pre-check and before storing")
    void register_normalizesSemver() {
        // the pre-check and the stored row must both use the canonical "1.2.0", never the raw "01.2.0"
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.2.0")).thenReturn(Optional.empty());
        when(rubricRepository.findById("piqi.core")).thenReturn(Optional.empty());
        when(rubricRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
        when(versionRepository.saveAndFlush(any())).thenAnswer(inv -> inv.getArgument(0));

        RubricVersionPayloadDto payload = RubricVersionPayloadDto.builder()
                .id("piqi.core")
                .semver("01.2.0")
                .title("PIQI Core")
                .checks(List.of(CheckDto.builder()
                        .id("c1").type(CheckType.FHIRPATH).dimension(PiqiDimension.CONFORMANCE)
                        .ordinal(1).enabled(true).build()))
                .build();

        RubricVersion result = service().registerVersion(payload, "qa");

        assertThat(result.getSemver()).isEqualTo("1.2.0");
        verify(versionRepository).findByRubricIdAndSemver("piqi.core", "1.2.0");
    }

    @Test
    @DisplayName("getVersion canonicalizes a leading-zero semver before the repository lookup")
    void getVersion_normalizesSemver() {
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.2.0"))
                .thenReturn(Optional.of(versionOf("piqi.core", "1.2.0", RubricVersionStatus.PUBLISHED)));

        RubricVersion result = service().getVersion("piqi.core", "01.2.0");

        assertThat(result.getSemver()).isEqualTo("1.2.0");
        verify(versionRepository).findByRubricIdAndSemver("piqi.core", "1.2.0");
    }
}


