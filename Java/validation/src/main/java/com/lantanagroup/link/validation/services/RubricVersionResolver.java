package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.RubricVersionSnapshot;
import com.lantanagroup.link.validation.models.Semver;
import com.lantanagroup.link.validation.providers.RubricCacheService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

import java.util.List;

@Service
@RequiredArgsConstructor
public class RubricVersionResolver {

    private final RubricCacheService cacheService;

    // version + its live checks in one lookup. Both come off the cached snapshot and are
    // detached — never save() them.
    public record ResolvedRubric(RubricVersion version, List<RubricCheck> checks) {}

    public ResolvedRubric resolve(String rubricId, String semver, boolean publishedOnly) {
        if (semver != null && !semver.isBlank()) {
            // "01.2.0" and "1.2.0" are the same registered version — canonicalize here too so the
            // 404/409 messages below echo the canonical form (the cache normalizes the key itself)
            semver = Semver.normalize(semver);
            RubricVersionSnapshot snapshot = cacheService.getVersion(rubricId, semver);
            if (snapshot == null) {
                throw new RubricVersionNotFoundException(rubricId, semver);
            }
            if (publishedOnly && snapshot.getStatus() != RubricVersionStatus.PUBLISHED) {
                // fetched-then-checked (rather than a status-scoped query) so the caller gets a clear
                // 409 "not PUBLISHED" instead of a misleading 404.
                throw new RubricLifecycleException(rubricId, semver, snapshot.getStatus(), "evaluate");
            }
            return new ResolvedRubric(snapshot.toVersionEntity(), snapshot.toCheckEntities());
        }

        // no semver requested → newest PUBLISHED version, via the cached latest pointer
        String latest = cacheService.getLatestPublishedSemver(rubricId);
        RubricVersionSnapshot snapshot = (latest == null) ? null : cacheService.getVersion(rubricId, latest);
        if (snapshot == null || snapshot.getStatus() != RubricVersionStatus.PUBLISHED) {
            // stale cache (swallowed evict, or a race with retire/publish) — drop it and
            // recompute from the DB. Latest must never serve a retired version.
            if (snapshot != null) {
                cacheService.evictVersion(rubricId, latest);
            } else {
                cacheService.evictLatestPointer(rubricId);
            }
            latest = cacheService.getLatestPublishedSemver(rubricId);
            snapshot = (latest == null) ? null : cacheService.getVersion(rubricId, latest);
        }
        if (snapshot == null || snapshot.getStatus() != RubricVersionStatus.PUBLISHED) {
            // same invariant as before caching: this branch only ever returns PUBLISHED
            throw new RubricVersionNotFoundException(rubricId, "latest-published");
        }
        return new ResolvedRubric(snapshot.toVersionEntity(), snapshot.toCheckEntities());
    }
}
