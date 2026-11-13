package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.RubricDryRunConfig;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricPreconditionFailedException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.providers.RubricCacheService;
import com.lantanagroup.link.validation.records.RubricSavePrecondition;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricLifecycleEventRepository;
import com.lantanagroup.link.validation.repositories.RubricRepository;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.mockito.InOrder;

import java.util.List;
import java.util.Map;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.inOrder;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

/**
 * The registry behaviour the rubric management interface relies on: saves that refuse to replace work the
 * caller has not seen, checking a definition without saving it, deleting a draft, and check counts.
 * {@link RubricRegistryServiceTest} covers register, publish and retire.
 */
class RubricRegistryServiceManagementTest {

    private final RubricRepository rubricRepository = mock(RubricRepository.class);
    private final RubricVersionRepository versionRepository = mock(RubricVersionRepository.class);
    private final RubricCheckRepository checkRepository = mock(RubricCheckRepository.class);
    private final RubricLifecycleEventRepository eventRepository = mock(RubricLifecycleEventRepository.class);
    private final RubricDefinitionValidator definitionValidator = mock(RubricDefinitionValidator.class);
    private final RubricCacheService cacheService = mock(RubricCacheService.class);

    private RubricRegistryService service() {
        return new RubricRegistryService(
                rubricRepository, versionRepository, checkRepository, eventRepository,
                definitionValidator, new ObjectMapper(), new RubricDryRunConfig(), cacheService);
    }

    private static RubricVersion version(RubricVersionStatus status) {
        return RubricVersion.builder()
                .rubricVersionId(1L).rubricId("piqi.core").semver("1.0.0").status(status).checksum("abc123").build();
    }

    private static RubricVersionPayloadDto payload() {
        return RubricVersionPayloadDto.builder()
                .id("piqi.core")
                .semver("1.0.0")
                .title("PIQI Core")
                .checks(List.of(CheckDto.builder()
                        .id("c1").type(CheckType.FHIRPATH).dimension(PiqiDimension.CONFORMANCE).ordinal(1).enabled(true)
                        .build()))
                .build();
    }

    private void stubExisting(RubricVersion existing) {
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0")).thenReturn(Optional.ofNullable(existing));
    }

    private void stubSave() {
        when(rubricRepository.findById("piqi.core"))
                .thenReturn(Optional.of(Rubric.builder().rubricId("piqi.core").title("PIQI Core").build()));
        when(rubricRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
        when(versionRepository.save(any())).thenAnswer(inv -> inv.getArgument(0));
        when(versionRepository.saveAndFlush(any())).thenAnswer(inv -> {
            RubricVersion saved = inv.getArgument(0);
            saved.setRubricVersionId(2L);
            return saved;
        });
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("registerVersion with a precondition")
    class SavePreconditions {

        @Test
        @DisplayName("the expected checksum matches the stored draft -> the draft is replaced")
        void expectedChecksumMatches() {
            stubExisting(version(RubricVersionStatus.DRAFT));
            stubSave();

            RubricVersion saved = service().registerVersion(payload(), "author", new RubricSavePrecondition("abc123", false));

            assertThat(saved.getDefinitionJson()).contains("c1");
            assertThat(saved.getChecksum()).isNotEqualTo("abc123");
        }

        @Test
        @DisplayName("the checksum is compared without regard to case")
        void expectedChecksumIgnoresCase() {
            stubExisting(version(RubricVersionStatus.DRAFT));
            stubSave();

            service().registerVersion(payload(), "author", new RubricSavePrecondition("ABC123", false));

            verify(versionRepository).save(any());
        }

        @Test
        @DisplayName("the stored draft has another checksum -> refused, and nothing is written")
        void expectedChecksumDiffers() {
            stubExisting(version(RubricVersionStatus.DRAFT));

            assertThatThrownBy(() -> service().registerVersion(payload(), "author", new RubricSavePrecondition("stale", false)))
                    .isInstanceOf(RubricPreconditionFailedException.class)
                    .hasMessageContaining("has been changed since it was read");

            verifyNothingWritten();
        }

        @Test
        @DisplayName("a checksum was expected but the draft is gone -> refused, not created anew")
        void expectedChecksumButDeleted() {
            stubExisting(null);

            assertThatThrownBy(() -> service().registerVersion(payload(), "author", new RubricSavePrecondition("abc123", false)))
                    .isInstanceOf(RubricPreconditionFailedException.class)
                    .hasMessageContaining("no longer exists");

            verifyNothingWritten();
        }

        @Test
        @DisplayName("must-not-exist and there is no such version -> created")
        void mustNotExistAndDoesNot() {
            stubExisting(null);
            stubSave();

            RubricVersion saved = service().registerVersion(payload(), "author", new RubricSavePrecondition(null, true));

            assertThat(saved.getStatus()).isEqualTo(RubricVersionStatus.DRAFT);
            verify(versionRepository).saveAndFlush(any());
        }

        @Test
        @DisplayName("must-not-exist but a draft is already there -> refused, the draft is left alone")
        void mustNotExistButDraftExists() {
            stubExisting(version(RubricVersionStatus.DRAFT));

            assertThatThrownBy(() -> service().registerVersion(payload(), "author", new RubricSavePrecondition(null, true)))
                    .isInstanceOf(RubricPreconditionFailedException.class)
                    .hasMessageContaining("already exists");

            verifyNothingWritten();
        }

        @Test
        @DisplayName("a published version is refused as immutable whatever the precondition says")
        void publishedIsStillAConflict() {
            stubExisting(version(RubricVersionStatus.PUBLISHED));

            assertThatThrownBy(() -> service().registerVersion(payload(), "author", new RubricSavePrecondition("abc123", true)))
                    .isInstanceOf(RubricVersionConflictException.class);
        }

        @Test
        @DisplayName("no precondition, or a null one, replaces a draft whatever it holds")
        void noPrecondition() {
            stubExisting(version(RubricVersionStatus.DRAFT));
            stubSave();

            service().registerVersion(payload(), "author", RubricSavePrecondition.NONE);
            service().registerVersion(payload(), "author", null);
            service().registerVersion(payload(), "author");

            verify(versionRepository, org.mockito.Mockito.times(3)).save(any());
        }

        @Test
        @DisplayName("an invalid definition is refused before the precondition is looked at")
        void validatesFirst() {
            doThrow(new InvalidRubricDefinitionException("Invalid rubric definition", List.of("checks: nope")))
                    .when(definitionValidator).validate(any());

            assertThatThrownBy(() -> service().registerVersion(payload(), "author", new RubricSavePrecondition("stale", false)))
                    .isInstanceOf(InvalidRubricDefinitionException.class);

            verifyNoInteractions(versionRepository);
        }

        private void verifyNothingWritten() {
            verify(versionRepository, never()).save(any());
            verify(versionRepository, never()).saveAndFlush(any());
            verify(rubricRepository, never()).save(any());
            verify(checkRepository, never()).softDeleteByRubricVersionId(any());
            verify(checkRepository, never()).saveAll(any());
            verifyNoInteractions(eventRepository, cacheService);
        }
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("preview")
    class Preview {

        @Test
        @DisplayName("gives the checksum registering would store, and writes nothing")
        void checksumMatchesRegister() {
            stubExisting(null);
            stubSave();
            String previewed = service().preview(payload()).checksum();

            RubricVersion registered = service().registerVersion(payload(), "author");

            assertThat(previewed).isEqualTo(registered.getChecksum()).hasSize(64);
        }

        @Test
        @DisplayName("writes nothing")
        void writesNothing() {
            stubExisting(null);

            service().preview(payload());

            verify(versionRepository, never()).save(any());
            verify(versionRepository, never()).saveAndFlush(any());
            verify(rubricRepository, never()).save(any());
            verifyNoInteractions(checkRepository, eventRepository, cacheService);
        }

        @Test
        @DisplayName("reports the version already registered under the id and semver, and that the rubric exists")
        void reportsExisting() {
            RubricVersion existing = version(RubricVersionStatus.PUBLISHED);
            stubExisting(existing);
            when(rubricRepository.existsById("piqi.core")).thenReturn(true);

            RubricRegistryService.DefinitionPreview preview = service().preview(payload());

            assertThat(preview.existing()).isSameAs(existing);
            assertThat(preview.rubricExists()).isTrue();
        }

        @Test
        @DisplayName("puts the semver in canonical form before looking the version up")
        void normalizesSemver() {
            stubExisting(version(RubricVersionStatus.DRAFT));
            RubricVersionPayloadDto payload = payload();
            payload.setSemver("01.0.0");

            RubricRegistryService.DefinitionPreview preview = service().preview(payload);

            assertThat(payload.getSemver()).isEqualTo("1.0.0");
            assertThat(preview.existing()).isNotNull();
        }

        @Test
        @DisplayName("an invalid definition is refused with the validator's errors")
        void invalid() {
            doThrow(new InvalidRubricDefinitionException("Invalid rubric definition", List.of("checks: nope")))
                    .when(definitionValidator).validate(any());

            assertThatThrownBy(() -> service().preview(payload()))
                    .isInstanceOf(InvalidRubricDefinitionException.class)
                    .satisfies(e -> assertThat(((InvalidRubricDefinitionException) e).getErrors()).containsExactly("checks: nope"));
        }

        @Test
        @DisplayName("existingVersion does not fail on a payload without an id or a semver")
        void existingVersionToleratesIncompletePayloads() {
            assertThat(service().existingVersion(new RubricVersionPayloadDto())).isEmpty();
            assertThat(service().existingVersion(RubricVersionPayloadDto.builder().id(" ").semver("1.0.0").build())).isEmpty();
            assertThat(service().existingVersion(RubricVersionPayloadDto.builder().id("piqi.core").build())).isEmpty();
            assertThat(service().rubricExists(null)).isFalse();
            assertThat(service().rubricExists(" ")).isFalse();
            verifyNoInteractions(versionRepository, rubricRepository);
        }
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("deleteDraft")
    class DeleteDraft {

        @Test
        @DisplayName("a draft among other versions: its checks and the version go, and the deletion is in the history")
        void deletesAndRecords() {
            stubExisting(version(RubricVersionStatus.DRAFT));
            when(versionRepository.deleteIfStatus(1L, RubricVersionStatus.DRAFT)).thenReturn(1);
            when(versionRepository.countByRubricId("piqi.core")).thenReturn(2L);

            service().deleteDraft("piqi.core", "1.0.0", "author");

            // the checks reference the version, so they go first
            InOrder order = inOrder(checkRepository, versionRepository);
            order.verify(checkRepository).deleteByRubricVersionId(1L);
            order.verify(versionRepository).deleteIfStatus(1L, RubricVersionStatus.DRAFT);

            ArgumentCaptor<RubricLifecycleEvent> event = ArgumentCaptor.forClass(RubricLifecycleEvent.class);
            verify(eventRepository).save(event.capture());
            assertThat(event.getValue().getAction()).isEqualTo(RubricLifecycleAction.DELETED);
            assertThat(event.getValue().getRubricId()).isEqualTo("piqi.core");
            assertThat(event.getValue().getSemver()).isEqualTo("1.0.0");
            assertThat(event.getValue().getActor()).isEqualTo("author");
            assertThat(event.getValue().getChecksum()).isEqualTo("abc123");

            verify(rubricRepository, never()).deleteById(any());
            verify(eventRepository, never()).deleteByRubricId(any());
            verify(cacheService).evictVersion("piqi.core", "1.0.0");
        }

        @Test
        @DisplayName("the rubric's only version: the rubric and its history go too, history first")
        void lastVersionRemovesTheRubric() {
            stubExisting(version(RubricVersionStatus.DRAFT));
            when(versionRepository.deleteIfStatus(1L, RubricVersionStatus.DRAFT)).thenReturn(1);
            when(versionRepository.countByRubricId("piqi.core")).thenReturn(0L);

            service().deleteDraft("piqi.core", "1.0.0", "author");

            InOrder order = inOrder(eventRepository, rubricRepository);
            order.verify(eventRepository).deleteByRubricId("piqi.core");
            order.verify(rubricRepository).deleteById("piqi.core");
            // no event for a rubric that no longer exists
            verify(eventRepository, never()).save(any());
            verify(cacheService).evictVersion("piqi.core", "1.0.0");
        }

        @Test
        @DisplayName("a published or retired version is refused, and nothing is deleted")
        void refusesNonDrafts() {
            for (RubricVersionStatus status : List.of(RubricVersionStatus.PUBLISHED, RubricVersionStatus.RETIRED)) {
                stubExisting(version(status));

                assertThatThrownBy(() -> service().deleteDraft("piqi.core", "1.0.0", "author"))
                        .isInstanceOf(RubricLifecycleException.class)
                        .hasMessage("Cannot delete piqi.core v1.0.0: status is " + status);
            }

            verify(checkRepository, never()).deleteByRubricVersionId(any());
            verify(versionRepository, never()).deleteIfStatus(any(), any());
            verifyNoInteractions(eventRepository, cacheService);
        }

        @Test
        @DisplayName("published between the read and the delete -> refused with its real status, nothing recorded")
        void losesTheRaceToPublish() {
            when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                    .thenReturn(Optional.of(version(RubricVersionStatus.DRAFT)))
                    .thenReturn(Optional.of(version(RubricVersionStatus.PUBLISHED)));
            when(versionRepository.deleteIfStatus(anyLong(), any())).thenReturn(0);

            assertThatThrownBy(() -> service().deleteDraft("piqi.core", "1.0.0", "author"))
                    .isInstanceOf(RubricLifecycleException.class)
                    .hasMessageContaining("status is PUBLISHED");

            verify(rubricRepository, never()).deleteById(any());
            verifyNoInteractions(eventRepository, cacheService);
        }

        @Test
        @DisplayName("unknown version -> not found")
        void notFound() {
            stubExisting(null);

            assertThatThrownBy(() -> service().deleteDraft("piqi.core", "1.0.0", "author"))
                    .isInstanceOf(RubricVersionNotFoundException.class);
        }

        @Test
        @DisplayName("the semver is put in canonical form before the version is looked up")
        void normalizesSemver() {
            stubExisting(version(RubricVersionStatus.DRAFT));
            when(versionRepository.deleteIfStatus(1L, RubricVersionStatus.DRAFT)).thenReturn(1);
            when(versionRepository.countByRubricId("piqi.core")).thenReturn(1L);

            service().deleteDraft("piqi.core", "01.0.0", "author");

            verify(cacheService).evictVersion("piqi.core", "1.0.0");
        }
    }

    // ---------------------------------------------------------------------------------------------
    @Test
    @DisplayName("checkCounts maps each version to its number of checks, and asks nothing for no versions")
    void checkCounts() {
        when(checkRepository.countLiveByRubricVersionIdIn(List.of(1L, 2L, 3L)))
                .thenReturn(List.of(new Object[]{1L, 97L}, new Object[]{3L, 4L}));

        assertThat(service().checkCounts(List.of(1L, 2L, 3L))).isEqualTo(Map.of(1L, 97, 3L, 4));
        assertThat(service().checkCounts(List.of())).isEmpty();
        verify(checkRepository, never()).countLiveByRubricVersionIdIn(List.of());
    }
}
