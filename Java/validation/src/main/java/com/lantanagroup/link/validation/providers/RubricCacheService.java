package com.lantanagroup.link.validation.providers;

import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.models.RubricVersionSnapshot;
import com.lantanagroup.link.validation.models.Semver;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.cache.Cache;
import org.springframework.cache.CacheManager;
import org.springframework.cache.annotation.Cacheable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionSynchronization;
import org.springframework.transaction.support.TransactionSynchronizationManager;

/**
 * Cache wrappers for the rubric lookups on the evaluate path. Lifecycle changes go through
 * {@link #evictVersion} so they take effect right away instead of waiting out the TTL.
 */
@Service
@RequiredArgsConstructor
public class RubricCacheService {

    private static final Logger logger = LoggerFactory.getLogger(RubricCacheService.class);

    public static final String VERSION_CACHE = "rubricVersionCache";
    public static final String LATEST_SEMVER_CACHE = "rubricLatestSemverCache";

    private final RubricVersionRepository rubricVersionRepository;
    private final RubricCheckRepository rubricCheckRepository;
    private final CacheManager cacheManager;

    // null = not found, and nulls are never cached, so a version registered right after a
    // miss shows up immediately. The 404-vs-409 decisions stay in the resolver.
    // Semver.normalize in the key AND the lookup so "01.2.0" and "1.2.0" resolve to one cache entry
    // and one row no matter which caller reached here — the cache can't be split by a caller that
    // forgot to canonicalize the semver first.
    @Cacheable(value = VERSION_CACHE,
            key = "#rubricId + ':' + T(com.lantanagroup.link.validation.models.Semver).normalize(#semver)",
            unless = "#result == null")
    public RubricVersionSnapshot getVersion(String rubricId, String semver) {
        return rubricVersionRepository.findByRubricIdAndSemver(rubricId, Semver.normalize(semver))
                .map(v -> RubricVersionSnapshot.from(v,
                        rubricCheckRepository.findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(
                                v.getRubricVersionId())))
                .orElse(null);
    }

    // latest PUBLISHED semver, i.e. what an evaluate call without a version gets. Sorted
    // semantically (1.10.0 > 1.9.0) — the DB stores semver as a string, lexical order is wrong.
    @Cacheable(value = LATEST_SEMVER_CACHE, key = "#rubricId", unless = "#result == null")
    public String getLatestPublishedSemver(String rubricId) {
        return rubricVersionRepository.findByRubricIdAndStatus(rubricId, RubricVersionStatus.PUBLISHED)
                .stream()
                .max(Semver.versionComparator())
                .map(RubricVersion::getSemver)
                .orElse(null);
    }

    // Called on register/publish/retire. The latest pointer goes too since publish/retire can
    // change the latest-published answer (on register it's just a harmless extra evict).
    // recordDryRun doesn't need this — the snapshot has no dry-run fields and the publish
    // gate reads the DB.
    //
    // Deferred to after-commit when a transaction is active, otherwise a concurrent reader
    // could re-cache the pre-commit row. Done by hand rather than @CacheEvict on a
    // transactionAware() manager: the decorator's deferred evict runs outside the cache
    // error handler, so a redis outage would 500 a retire whose commit already went through.
    public void evictVersion(String rubricId, String semver) {
        // normalize to match getVersion's cache key, so an evict for "01.2.0" still clears the
        // entry cached under the canonical "1.2.0" regardless of what the caller passed
        String key = rubricId + ":" + Semver.normalize(semver);
        afterCommitOrNow(() -> {
            safeEvict(VERSION_CACHE, key);
            safeEvict(LATEST_SEMVER_CACHE, rubricId);
        });
    }

    // for the resolver, when the latest pointer turns out to be stale
    public void evictLatestPointer(String rubricId) {
        afterCommitOrNow(() -> safeEvict(LATEST_SEMVER_CACHE, rubricId));
    }

    private static void afterCommitOrNow(Runnable eviction) {
        if (TransactionSynchronizationManager.isSynchronizationActive()) {
            TransactionSynchronizationManager.registerSynchronization(new TransactionSynchronization() {
                @Override
                public void afterCommit() {
                    eviction.run();
                }
            });
        } else {
            eviction.run();
        }
    }

    private void safeEvict(String cacheName, Object key) {
        try {
            Cache cache = cacheManager.getCache(cacheName);
            if (cache != null) {
                cache.evict(key);
            }
        } catch (Exception e) {
            // swallow — the entry falls out via TTL, and the resolver repairs a stale
            // latest pointer sooner than that
            logger.warn("Cache evict {}::{} failed; entry will expire via TTL: {}",
                    cacheName, key, e.getMessage());
        }
    }
}
