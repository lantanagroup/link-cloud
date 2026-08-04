package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricLifecycleEventRepository;
import com.lantanagroup.link.validation.repositories.RubricRepository;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.dao.DataIntegrityViolationException;

import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class RubricRegistryServiceTest {

    @Test
    @DisplayName("losing a concurrent registration race -> 409 conflict, not a raw integrity violation")
    void concurrentRegistrationTranslatesToConflict() {
        RubricRepository rubricRepository = mock(RubricRepository.class);
        RubricVersionRepository versionRepository = mock(RubricVersionRepository.class);
        RubricCheckRepository checkRepository = mock(RubricCheckRepository.class);
        RubricLifecycleEventRepository eventRepository = mock(RubricLifecycleEventRepository.class);
        RubricDefinitionValidator definitionValidator = mock(RubricDefinitionValidator.class);

        RubricRegistryService service = new RubricRegistryService(
                rubricRepository, versionRepository, checkRepository, eventRepository,
                definitionValidator, new ObjectMapper());

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

        assertThatThrownBy(() -> service.registerVersion(payload, "qa"))
                .isInstanceOf(RubricVersionConflictException.class);
    }
}
