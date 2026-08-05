package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;

class RubricVersionResolverTest {

    private final RubricVersionRepository repository = mock(RubricVersionRepository.class);
    private final RubricVersionResolver resolver = new RubricVersionResolver(repository);

    private static RubricVersion version(String semver, RubricVersionStatus status) {
        return RubricVersion.builder()
                .rubricId("piqi.core")
                .semver(semver)
                .status(status)
                .build();
    }

    @Test
    @DisplayName("an explicit semver resolves that exact version")
    void resolvesExplicitSemver() {
        RubricVersion v = version("1.2.0", RubricVersionStatus.PUBLISHED);
        when(repository.findByRubricIdAndSemver("piqi.core", "1.2.0")).thenReturn(Optional.of(v));

        assertThat(resolver.resolve("piqi.core", "1.2.0", true)).isSameAs(v);
        // an explicit semver must not fall through to the latest-published lookup
        verify(repository).findByRubricIdAndSemver("piqi.core", "1.2.0");
        verifyNoMoreInteractions(repository);
    }

    @Test
    @DisplayName("an explicit semver that does not exist throws RubricVersionNotFoundException")
    void explicitSemverNotFound() {
        when(repository.findByRubricIdAndSemver("piqi.core", "9.9.9")).thenReturn(Optional.empty());

        assertThatThrownBy(() -> resolver.resolve("piqi.core", "9.9.9", true))
                .isInstanceOf(RubricVersionNotFoundException.class);
    }

    @Test
    @DisplayName("$evaluate (publishedOnly) rejects an explicit non-PUBLISHED version with a lifecycle conflict")
    void evaluateRejectsNonPublishedExplicitVersion() {
        RubricVersion draft = version("1.2.0", RubricVersionStatus.DRAFT);
        when(repository.findByRubricIdAndSemver("piqi.core", "1.2.0")).thenReturn(Optional.of(draft));

        assertThatThrownBy(() -> resolver.resolve("piqi.core", "1.2.0", true))
                .isInstanceOf(RubricLifecycleException.class);
    }

    @Test
    @DisplayName("$dry-run (publishedOnly=false) resolves an explicit DRAFT version")
    void dryRunAllowsDraftExplicitVersion() {
        RubricVersion draft = version("1.2.0", RubricVersionStatus.DRAFT);
        when(repository.findByRubricIdAndSemver("piqi.core", "1.2.0")).thenReturn(Optional.of(draft));

        assertThat(resolver.resolve("piqi.core", "1.2.0", false)).isSameAs(draft);
    }

    @Test
    @DisplayName("no semver -> newest PUBLISHED version, ordered semantically (1.10.0 > 1.9.0)")
    void resolvesLatestPublishedSemantically() {
        when(repository.findByRubricIdAndStatus("piqi.core", RubricVersionStatus.PUBLISHED))
                .thenReturn(List.of(
                        version("1.9.0", RubricVersionStatus.PUBLISHED),
                        version("1.10.0", RubricVersionStatus.PUBLISHED),
                        version("1.2.0", RubricVersionStatus.PUBLISHED)));

        RubricVersion resolved = resolver.resolve("piqi.core", null, true);

        assertThat(resolved.getSemver()).isEqualTo("1.10.0");
    }

    @Test
    @DisplayName("a blank semver is treated as 'latest published'")
    void blankSemverUsesLatestPublished() {
        when(repository.findByRubricIdAndStatus(eq("piqi.core"), eq(RubricVersionStatus.PUBLISHED)))
                .thenReturn(List.of(version("2.0.0", RubricVersionStatus.PUBLISHED)));

        assertThat(resolver.resolve("piqi.core", "   ", true).getSemver()).isEqualTo("2.0.0");
    }

    @Test
    @DisplayName("no published version -> RubricVersionNotFoundException")
    void noPublishedVersionThrows() {
        when(repository.findByRubricIdAndStatus("piqi.core", RubricVersionStatus.PUBLISHED))
                .thenReturn(List.of());

        assertThatThrownBy(() -> resolver.resolve("piqi.core", null, true))
                .isInstanceOf(RubricVersionNotFoundException.class);
    }
}
