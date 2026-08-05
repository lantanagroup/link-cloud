package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.RubricDryRunConfig;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricDryRunRequiredException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
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
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class RubricRegistryServiceTest {

    private final RubricRepository rubricRepository = mock(RubricRepository.class);
    private final RubricVersionRepository versionRepository = mock(RubricVersionRepository.class);
    private final RubricCheckRepository checkRepository = mock(RubricCheckRepository.class);
    private final RubricLifecycleEventRepository eventRepository = mock(RubricLifecycleEventRepository.class);
    private final RubricDefinitionValidator definitionValidator = mock(RubricDefinitionValidator.class);
    private final RubricDryRunConfig dryRunConfig = new RubricDryRunConfig();

    private RubricRegistryService service() {
        return new RubricRegistryService(
                rubricRepository, versionRepository, checkRepository, eventRepository,
                definitionValidator, new ObjectMapper(), dryRunConfig);
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
}
