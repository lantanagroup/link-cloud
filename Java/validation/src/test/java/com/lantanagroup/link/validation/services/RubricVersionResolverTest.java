package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.RubricVersionSnapshot;
import com.lantanagroup.link.validation.providers.RubricCacheService;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;

class RubricVersionResolverTest {

    private final RubricCacheService cacheService = mock(RubricCacheService.class);
    private final RubricVersionResolver resolver = new RubricVersionResolver(cacheService);

    private static RubricVersionSnapshot snapshot(String semver, RubricVersionStatus status) {
        return RubricVersionSnapshot.builder()
                .rubricVersionId(UUID.randomUUID())
                .rubricId("piqi.core")
                .semver(semver)
                .status(status)
                .checksum("abc")
                .checks(List.of(RubricVersionSnapshot.CheckSnapshot.builder()
                        .checkId(UUID.randomUUID())
                        .checkLocalId("c1")
                        .type(CheckType.FHIRPATH)
                        .dimension(PiqiDimension.CONFORMANCE)
                        .enabled(true)
                        .build()))
                .build();
    }

    @Test
    @DisplayName("an explicit semver resolves that exact version with its checks")
    void resolvesExplicitSemver() {
        RubricVersionSnapshot snap = snapshot("1.2.0", RubricVersionStatus.PUBLISHED);
        when(cacheService.getVersion("piqi.core", "1.2.0")).thenReturn(snap);

        RubricVersionResolver.ResolvedRubric resolved = resolver.resolve("piqi.core", "1.2.0", true);

        assertThat(resolved.version().getSemver()).isEqualTo("1.2.0");
        assertThat(resolved.version().getRubricVersionId()).isEqualTo(snap.getRubricVersionId());
        assertThat(resolved.checks()).hasSize(1);
        assertThat(resolved.checks().get(0).getCheckLocalId()).isEqualTo("c1");
        // an explicit semver must not fall through to the latest-published lookup
        verify(cacheService).getVersion("piqi.core", "1.2.0");
        verifyNoMoreInteractions(cacheService);
    }

    @Test
    @DisplayName("an explicit semver that does not exist throws RubricVersionNotFoundException")
    void explicitSemverNotFound() {
        when(cacheService.getVersion("piqi.core", "9.9.9")).thenReturn(null);

        assertThatThrownBy(() -> resolver.resolve("piqi.core", "9.9.9", true))
                .isInstanceOf(RubricVersionNotFoundException.class);
    }

    @Test
    @DisplayName("$evaluate (publishedOnly) rejects an explicit non-PUBLISHED version with a lifecycle conflict")
    void evaluateRejectsNonPublishedExplicitVersion() {
        when(cacheService.getVersion("piqi.core", "1.2.0"))
                .thenReturn(snapshot("1.2.0", RubricVersionStatus.DRAFT));

        assertThatThrownBy(() -> resolver.resolve("piqi.core", "1.2.0", true))
                .isInstanceOf(RubricLifecycleException.class);
    }

    @Test
    @DisplayName("$dry-run (publishedOnly=false) resolves an explicit DRAFT version")
    void dryRunAllowsDraftExplicitVersion() {
        when(cacheService.getVersion("piqi.core", "1.2.0"))
                .thenReturn(snapshot("1.2.0", RubricVersionStatus.DRAFT));

        assertThat(resolver.resolve("piqi.core", "1.2.0", false).version().getStatus())
                .isEqualTo(RubricVersionStatus.DRAFT);
    }

    @Test
    @DisplayName("no semver -> latest published via the cached pointer")
    void resolvesLatestPublishedViaPointer() {
        when(cacheService.getLatestPublishedSemver("piqi.core")).thenReturn("1.10.0");
        when(cacheService.getVersion("piqi.core", "1.10.0"))
                .thenReturn(snapshot("1.10.0", RubricVersionStatus.PUBLISHED));

        RubricVersionResolver.ResolvedRubric resolved = resolver.resolve("piqi.core", null, true);

        assertThat(resolved.version().getSemver()).isEqualTo("1.10.0");
        // healthy pointer: no self-heal eviction should happen
        verify(cacheService, never()).evictLatestPointer("piqi.core");
    }

    @Test
    @DisplayName("a blank semver is treated as 'latest published'")
    void blankSemverUsesLatestPublished() {
        when(cacheService.getLatestPublishedSemver("piqi.core")).thenReturn("2.0.0");
        when(cacheService.getVersion("piqi.core", "2.0.0"))
                .thenReturn(snapshot("2.0.0", RubricVersionStatus.PUBLISHED));

        assertThat(resolver.resolve("piqi.core", "   ", true).version().getSemver()).isEqualTo("2.0.0");
    }

    @Test
    @DisplayName("no published version -> RubricVersionNotFoundException")
    void noPublishedVersionThrows() {
        when(cacheService.getLatestPublishedSemver("piqi.core")).thenReturn(null);

        assertThatThrownBy(() -> resolver.resolve("piqi.core", null, true))
                .isInstanceOf(RubricVersionNotFoundException.class);
        // the null pointer triggers one self-heal retry before giving up
        verify(cacheService).evictLatestPointer("piqi.core");
    }

    @Test
    @DisplayName("self-heal: a stale pointer to a RETIRED version is evicted and the real latest is served")
    void selfHealsStalePointerToRetiredVersion() {
        // stale pointer says 1.2.0, but that snapshot is RETIRED; the recomputed answer is 1.1.0
        when(cacheService.getLatestPublishedSemver("piqi.core")).thenReturn("1.2.0", "1.1.0");
        when(cacheService.getVersion("piqi.core", "1.2.0"))
                .thenReturn(snapshot("1.2.0", RubricVersionStatus.RETIRED));
        when(cacheService.getVersion("piqi.core", "1.1.0"))
                .thenReturn(snapshot("1.1.0", RubricVersionStatus.PUBLISHED));

        RubricVersionResolver.ResolvedRubric resolved = resolver.resolve("piqi.core", null, true);

        assertThat(resolved.version().getSemver()).isEqualTo("1.1.0");
        // both the stale snapshot and the pointer must be dropped
        verify(cacheService).evictVersion("piqi.core", "1.2.0");
    }

    @Test
    @DisplayName("self-heal: a pointer to a missing snapshot is evicted and the real latest is served")
    void selfHealsDanglingPointer() {
        when(cacheService.getLatestPublishedSemver("piqi.core")).thenReturn("1.2.0", "1.1.0");
        when(cacheService.getVersion("piqi.core", "1.2.0")).thenReturn(null);
        when(cacheService.getVersion("piqi.core", "1.1.0"))
                .thenReturn(snapshot("1.1.0", RubricVersionStatus.PUBLISHED));

        assertThat(resolver.resolve("piqi.core", null, true).version().getSemver()).isEqualTo("1.1.0");
        verify(cacheService).evictLatestPointer("piqi.core");
    }

    @Test
    @DisplayName("self-heal that still finds nothing published -> RubricVersionNotFoundException")
    void selfHealExhaustedThrows() {
        // the only version was just retired: stale pointer on the first read, nothing after
        when(cacheService.getLatestPublishedSemver("piqi.core")).thenReturn("1.0.0", (String) null);
        when(cacheService.getVersion("piqi.core", "1.0.0"))
                .thenReturn(snapshot("1.0.0", RubricVersionStatus.RETIRED));

        assertThatThrownBy(() -> resolver.resolve("piqi.core", null, true))
                .isInstanceOf(RubricVersionNotFoundException.class);
    }

    @Test
    @DisplayName("latest-published never yields a non-PUBLISHED version, even if the retried snapshot is stale")
    void latestNeverReturnsNonPublished() {
        // pathological double-staleness: even after self-heal the snapshot claims RETIRED
        when(cacheService.getLatestPublishedSemver("piqi.core")).thenReturn("1.0.0");
        when(cacheService.getVersion("piqi.core", "1.0.0"))
                .thenReturn(snapshot("1.0.0", RubricVersionStatus.RETIRED));

        assertThatThrownBy(() -> resolver.resolve("piqi.core", null, true))
                .isInstanceOf(RubricVersionNotFoundException.class);
    }

    @Test
    @DisplayName("an explicit leading-zero semver is canonicalized before the cache lookup")
    void resolvesNormalizedSemver() {
        when(cacheService.getVersion("piqi.core", "1.2.0"))
                .thenReturn(snapshot("1.2.0", RubricVersionStatus.PUBLISHED));

        RubricVersionResolver.ResolvedRubric resolved = resolver.resolve("piqi.core", "01.2.0", true);

        assertThat(resolved.version().getSemver()).isEqualTo("1.2.0");
        // the raw "01.2.0" must never reach the cache — only its canonical form
        verify(cacheService).getVersion("piqi.core", "1.2.0");
    }
}
