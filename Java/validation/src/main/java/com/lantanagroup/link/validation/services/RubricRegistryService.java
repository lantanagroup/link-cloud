package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.RubricDryRunConfig;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricDryRunRequiredException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricNotFoundException;
import com.lantanagroup.link.validation.exceptions.RubricPreconditionFailedException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.models.Semver;
import com.lantanagroup.link.validation.providers.RubricCacheService;
import com.lantanagroup.link.validation.records.RubricSavePrecondition;
import com.lantanagroup.link.validation.records.RubricSearch;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricLifecycleEventRepository;
import com.lantanagroup.link.validation.repositories.RubricRepository;
import com.lantanagroup.link.validation.repositories.RubricSpecifications;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import com.lantanagroup.link.shared.utils.LogUtils;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.HexFormat;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
public class RubricRegistryService {

    private static final Logger logger = LoggerFactory.getLogger(RubricRegistryService.class);

    private final RubricRepository rubricRepository;
    private final RubricVersionRepository rubricVersionRepository;
    private final RubricCheckRepository rubricCheckRepository;
    private final RubricLifecycleEventRepository lifecycleEventRepository;
    private final RubricDefinitionValidator definitionValidator;
    private final ObjectMapper objectMapper;
    private final RubricDryRunConfig dryRunConfig;
    private final RubricCacheService rubricCacheService;

    @Transactional
    public RubricVersion registerVersion(RubricVersionPayloadDto payload, String actor) {
        return registerVersion(payload, actor, RubricSavePrecondition.NONE);
    }

    /**
     * Registers a version as a DRAFT, or replaces the DRAFT already registered under that semver.
     *
     * @param precondition what the caller says the version's state is. A save made on stale knowledge is
     *                     refused with {@link RubricPreconditionFailedException} instead of replacing a
     *                     draft the caller has not seen. {@link RubricSavePrecondition#NONE} replaces a
     *                     draft whatever it holds
     */
    @Transactional
    public RubricVersion registerVersion(RubricVersionPayloadDto payload, String actor,
                                         RubricSavePrecondition precondition) {
        definitionValidator.validate(payload);
        // "01.2.3" and "1.2.3" are the same version — normalize before it's ever looked up or stored
        payload.setSemver(Semver.normalize(payload.getSemver()));

        String definitionJson = writeJson(payload);
        String checksum = sha256(definitionJson);

        // re-registering a DRAFT replaces it in place. published/retired versions are
        // immutable, callers have to bump the semver
        Optional<RubricVersion> existing =
                rubricVersionRepository.findByRubricIdAndSemver(payload.getId(), payload.getSemver());
        if (existing.isPresent() && existing.get().getStatus() != RubricVersionStatus.DRAFT) {
            throw new RubricVersionConflictException(
                    payload.getId(), payload.getSemver(), existing.get().getStatus());
        }
        checkPrecondition(precondition, payload, existing);

        // The rubric row is identity only (id, title, owner). Declarative metadata (dimensions,
        // applicableContext, scoringPolicy) is version-scoped and lives on the
        // rubric_version row below. Refresh title/owner on each register so they track the newest
        // register instead of freezing at the first version.
        String title = payload.getTitle() != null ? payload.getTitle() : payload.getId();
        Rubric rubric = rubricRepository.findById(payload.getId()).orElse(null);
        if (rubric == null) {
            rubric = rubricRepository.save(Rubric.builder()
                    .rubricId(payload.getId())
                    .title(title)
                    .owner(payload.getOwner())
                    .build());
        } else {
            rubric.setTitle(title);
            rubric.setOwner(payload.getOwner());
            rubricRepository.save(rubric);
        }

        RubricVersion version;
        if (existing.isPresent()) {
            version = existing.get();
            boolean definitionChanged = !checksum.equals(version.getChecksum());
            version.setChecksum(checksum);
            version.setDefinitionJson(definitionJson);
            version.setDimensionsJson(writeJson(payload.getDimensions()));
            version.setApplicableContextJson(writeJson(payload.getApplicableContext()));
            version.setScoringPolicyJson(writeJson(payload.getScoringPolicy()));
            version.setCreatedBy(actor);
            if (definitionChanged) {
                // the old dry run was against a different definition, so it no longer counts
                version.setDryRunCompletedAt(null);
                version.setDryRunStatus(null);
            }
            version = rubricVersionRepository.save(version);
            // soft-delete the old checks, the new payload's checks replace them
            rubricCheckRepository.softDeleteByRubricVersionId(version.getRubricVersionId());
        } else {
            version = RubricVersion.builder()
                    .rubricId(rubric.getRubricId())
                    .semver(payload.getSemver())
                    .status(RubricVersionStatus.DRAFT)
                    .checksum(checksum)
                    .definitionJson(definitionJson)
                    .dimensionsJson(writeJson(payload.getDimensions()))
                    .applicableContextJson(writeJson(payload.getApplicableContext()))
                    .scoringPolicyJson(writeJson(payload.getScoringPolicy()))
                    .createdBy(actor)
                    .build();
            try {
                // flush so a concurrent registration that won the uq_rv_rubric_semver race
                // surfaces here rather than at commit, and reports the same 409 as the pre-check
                version = rubricVersionRepository.saveAndFlush(version);
            } catch (DataIntegrityViolationException e) {
                throw new RubricVersionConflictException(payload.getId(), payload.getSemver());
            }
        }

        List<RubricCheck> checks = new ArrayList<>();
        for (CheckDto c : payload.getChecks()) {
            checks.add(RubricCheck.builder()
                    .rubricVersionId(version.getRubricVersionId())
                    .checkLocalId(c.getId())
                    .type(c.getType())
                    .dimension(c.getDimension())
                    .parametersJson(writeJson(c.getParameters()))
                    .severityOverride(c.getSeverityOverride())
                    .ordinal(c.getOrdinal())
                    .enabled(c.isEnabled())
                    .build());
        }
        rubricCheckRepository.saveAll(checks);
        // a draft re-register replaces the definition and checks in place, so drop any
        // cached snapshot of this version
        rubricCacheService.evictVersion(payload.getId(), payload.getSemver());
        recordEvent(rubric.getRubricId(), payload.getSemver(), RubricLifecycleAction.REGISTERED, actor, checksum);
        logger.info("{} rubric {} v{} with {} checks (status=DRAFT)",
                existing.isPresent() ? "Re-registered (draft replaced)" : "Registered",
                LogUtils.sanitize(rubric.getRubricId()), LogUtils.sanitize(payload.getSemver()), checks.size());
        return version;
    }

    @Transactional
    public RubricVersion publish(String rubricId, String semver, String publishedBy) {
        final String normalizedSemver = Semver.normalize(semver);
        RubricVersion v = rubricVersionRepository.findByRubricIdAndSemver(rubricId, normalizedSemver)
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, normalizedSemver));
        if (v.getStatus() != RubricVersionStatus.DRAFT) {
            throw new RubricLifecycleException(rubricId, normalizedSemver, v.getStatus(), "publish");
        }
        if (dryRunConfig.isRequiredForPublish()) {
            // a completed dry run is required, but its business verdict is not ΓÇö a rubric that
            // executes cleanly and correctly flags an UNACCEPTABLE test payload must still be
            // publishable. only a dry run that never completed (dryRunCompletedAt == null, e.g. it
            // was never run, or the definition changed since, or execution itself threw) blocks this.
            if (v.getDryRunCompletedAt() == null) {
                throw new RubricDryRunRequiredException(rubricId, normalizedSemver);
            }
        }
        // the status check above can go stale before we write, so flip it with a guarded update
        // instead of setStatus + save. only one concurrent publish gets a row back.
        OffsetDateTime publishedAt = OffsetDateTime.now();
        int updated = rubricVersionRepository.publishIfStatus(
                v.getRubricVersionId(), RubricVersionStatus.DRAFT, RubricVersionStatus.PUBLISHED, publishedAt, publishedBy);
        if (updated == 0) {
            // someone else transitioned it first — re-read so the 409 reports the real status
            RubricVersion current = rubricVersionRepository.findByRubricIdAndSemver(rubricId, normalizedSemver)
                    .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, normalizedSemver));
            throw new RubricLifecycleException(rubricId, normalizedSemver, current.getStatus(), "publish");
        }
        // the update cleared the persistence context; patch the fields on the entity we return
        v.setStatus(RubricVersionStatus.PUBLISHED);
        v.setPublishedAt(publishedAt);
        v.setPublishedBy(publishedBy);
        // drop the stale DRAFT snapshot and the latest pointer — this may be the new latest
        rubricCacheService.evictVersion(rubricId, normalizedSemver);
        recordEvent(rubricId, normalizedSemver, RubricLifecycleAction.PUBLISHED, publishedBy, v.getChecksum());
        logger.info("Published rubric {} v{} by {}",
                LogUtils.sanitize(rubricId), LogUtils.sanitize(normalizedSemver), LogUtils.sanitize(publishedBy));
        return v;
    }

    @Transactional
    public RubricVersion retire(String rubricId, String semver, String retiredBy) {
        final String normalizedSemver = Semver.normalize(semver);
        RubricVersion v = rubricVersionRepository.findByRubricIdAndSemver(rubricId, normalizedSemver)
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, normalizedSemver));
        // drafts can be retired directly (abandoned without ever publishing), only
        // re-retiring is blocked so retiredAt/retiredBy and the audit trail stay intact
        if (v.getStatus() == RubricVersionStatus.RETIRED) {
            throw new RubricLifecycleException(rubricId, normalizedSemver, v.getStatus(), "retire");
        }
        // guarded update like publish — if it was retired between our read and here we get 0 rows
        OffsetDateTime retiredAt = OffsetDateTime.now();
        int updated = rubricVersionRepository.retireIfNotRetired(
                v.getRubricVersionId(), RubricVersionStatus.RETIRED, retiredAt, retiredBy);
        if (updated == 0) {
            RubricVersion current = rubricVersionRepository.findByRubricIdAndSemver(rubricId, normalizedSemver)
                    .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, normalizedSemver));
            throw new RubricLifecycleException(rubricId, normalizedSemver, current.getStatus(), "retire");
        }
        v.setStatus(RubricVersionStatus.RETIRED);
        v.setRetiredAt(retiredAt);
        v.setRetiredBy(retiredBy);
        // drop the cached entry and the latest pointer so nothing keeps evaluating this version
        rubricCacheService.evictVersion(rubricId, normalizedSemver);
        recordEvent(rubricId, normalizedSemver, RubricLifecycleAction.RETIRED, retiredBy, v.getChecksum());
        logger.info("Retired rubric {} v{} by {}",
                LogUtils.sanitize(rubricId), LogUtils.sanitize(normalizedSemver), LogUtils.sanitize(retiredBy));
        return v;
    }

    /**
     * Deletes a version that is still a DRAFT, with its checks. A draft was never published, so nothing
     * was ever evaluated against it and no result refers to it. When it was the rubric's only version the
     * rubric goes with it, history included; otherwise the deletion is recorded in the rubric's history.
     */
    @Transactional
    public void deleteDraft(String rubricId, String semver, String deletedBy) {
        final String normalizedSemver = Semver.normalize(semver);
        RubricVersion v = rubricVersionRepository.findByRubricIdAndSemver(rubricId, normalizedSemver)
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, normalizedSemver));
        if (v.getStatus() != RubricVersionStatus.DRAFT) {
            throw new RubricLifecycleException(rubricId, normalizedSemver, v.getStatus(), "delete");
        }
        String checksum = v.getChecksum();
        rubricCheckRepository.deleteByRubricVersionId(v.getRubricVersionId());
        // guarded like publish and retire: if it stopped being a DRAFT between our read and here, nothing
        // is deleted, and throwing rolls the checks' deletion back with the transaction
        int deleted = rubricVersionRepository.deleteIfStatus(v.getRubricVersionId(), RubricVersionStatus.DRAFT);
        if (deleted == 0) {
            RubricVersion current = rubricVersionRepository.findByRubricIdAndSemver(rubricId, normalizedSemver)
                    .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, normalizedSemver));
            throw new RubricLifecycleException(rubricId, normalizedSemver, current.getStatus(), "delete");
        }
        boolean lastVersion = rubricVersionRepository.countByRubricId(rubricId) == 0;
        if (lastVersion) {
            // a rubric with no versions is nothing; its history goes first, the events reference it
            lifecycleEventRepository.deleteByRubricId(rubricId);
            rubricRepository.deleteById(rubricId);
        } else {
            recordEvent(rubricId, normalizedSemver, RubricLifecycleAction.DELETED, deletedBy, checksum);
        }
        rubricCacheService.evictVersion(rubricId, normalizedSemver);
        logger.info("Deleted draft rubric {} v{} by {}{}",
                LogUtils.sanitize(rubricId), LogUtils.sanitize(normalizedSemver), LogUtils.sanitize(deletedBy),
                lastVersion ? " (its only version; the rubric was removed)" : "");
    }

    /**
     * What registering this definition would do, without doing it: runs the same rules, and reports the
     * checksum it would be stored with and the version already registered under its id and semver, if any.
     * The payload's semver is put in canonical form, as registering does.
     *
     * @throws com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException when registering would be refused
     */
    @Transactional(readOnly = true)
    public DefinitionPreview preview(RubricVersionPayloadDto payload) {
        definitionValidator.validate(payload);
        payload.setSemver(Semver.normalize(payload.getSemver()));
        return new DefinitionPreview(sha256(writeJson(payload)), existingVersion(payload).orElse(null),
                rubricExists(payload.getId()));
    }

    /**
     * The version already registered under a payload's id and semver, when both are usable enough to look
     * one up. Never throws on a malformed payload: it is also asked about definitions that failed validation.
     */
    @Transactional(readOnly = true)
    public Optional<RubricVersion> existingVersion(RubricVersionPayloadDto payload) {
        if (payload.getId() == null || payload.getId().isBlank() || payload.getSemver() == null) {
            return Optional.empty();
        }
        try {
            return rubricVersionRepository.findByRubricIdAndSemver(payload.getId(), Semver.normalize(payload.getSemver()));
        } catch (IllegalArgumentException e) {
            return Optional.empty(); // not a semver, so no version can be registered under it
        }
    }

    public boolean rubricExists(String rubricId) {
        return rubricId != null && !rubricId.isBlank() && rubricRepository.existsById(rubricId);
    }

    /**
     * @param checksum     the checksum the definition would be stored with
     * @param existing     the version already registered under the definition's id and semver, or null
     * @param rubricExists whether a rubric with the definition's id exists
     */
    public record DefinitionPreview(String checksum, RubricVersion existing, boolean rubricExists) {
    }

    /** Rubrics matching the search, paged and sorted as asked. */
    public Page<Rubric> searchRubrics(RubricSearch search, Pageable pageable) {
        return rubricRepository.findAll(RubricSpecifications.matching(search), pageable);
    }

    /** Every owner a rubric has, in order, for a filter to offer. */
    public List<String> listOwners() {
        return rubricRepository.findDistinctOwners();
    }

    /** How many checks each of these versions has. A version with none is absent from the map. */
    public Map<Long, Integer> checkCounts(Collection<Long> rubricVersionIds) {
        if (rubricVersionIds.isEmpty()) return Map.of();
        Map<Long, Integer> counts = new HashMap<>();
        for (Object[] row : rubricCheckRepository.countLiveByRubricVersionIdIn(rubricVersionIds)) {
            counts.put((Long) row[0], ((Number) row[1]).intValue());
        }
        return counts;
    }

    // status filter is optional: when set, only rubrics having at least one version in
    // that status come back
    public Page<Rubric> listRubrics(RubricVersionStatus status, Pageable pageable) {
        return status != null
                ? rubricRepository.findByVersionStatus(status, pageable)
                : rubricRepository.findAll(pageable);
    }

    public Rubric getRubric(String rubricId) {
        return rubricRepository.findById(rubricId)
                .orElseThrow(() -> new RubricNotFoundException(rubricId));
    }

    public Optional<String> latestPublishedSemver(String rubricId) {
        return rubricVersionRepository.findByRubricIdAndStatus(rubricId, RubricVersionStatus.PUBLISHED)
                .stream()
                .max(Semver.versionComparator())
                .map(RubricVersion::getSemver);
    }

    // Batch-load versions for a set of rubrics, grouped by rubric id and sorted newest-first per rubric.
    // Used by the rubric list endpoint to avoid an N+1 query. Optional status filter trims the
    // versions to that status only.
    public Map<String, List<RubricVersion>> versionsByRubricId(Collection<String> rubricIds,
                                                               RubricVersionStatus status) {
        if (rubricIds.isEmpty()) return Map.of();
        List<RubricVersion> versions = status != null
                ? rubricVersionRepository.findByRubricIdInAndStatus(rubricIds, status)
                : rubricVersionRepository.findByRubricIdIn(rubricIds);
        Map<String, List<RubricVersion>> grouped = versions.stream()
                .collect(Collectors.groupingBy(RubricVersion::getRubricId));
        grouped.values().forEach(list -> list.sort(Semver.versionComparator().reversed()));
        return grouped;
    }

    public List<RubricVersion> listVersions(String rubricId, RubricVersionStatus status) {
        if (!rubricRepository.existsById(rubricId)) {
            throw new RubricNotFoundException(rubricId);
        }
        List<RubricVersion> versions = status != null
                ? rubricVersionRepository.findByRubricIdAndStatus(rubricId, status)
                : rubricVersionRepository.findByRubricId(rubricId);
        return versions.stream().sorted(Semver.versionComparator().reversed()).toList();
    }

    public RubricVersion getVersion(String rubricId, String semver) {
        final String normalizedSemver = Semver.normalize(semver);
        return rubricVersionRepository.findByRubricIdAndSemver(rubricId, normalizedSemver)
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, normalizedSemver));
    }

    public Optional<RubricVersion> findVersion(String rubricId, String semver) {
        return rubricVersionRepository.findByRubricIdAndSemver(rubricId, Semver.normalize(semver));
    }

    public List<RubricCheck> getChecks(Long rubricVersionId) {
        return rubricCheckRepository.findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(rubricVersionId);
    }

    public List<RubricLifecycleEvent> listLifecycleEvents(String rubricId) {
        if (!rubricRepository.existsById(rubricId)) {
            throw new RubricNotFoundException(rubricId);
        }
        return lifecycleEventRepository.findByRubricIdOrderByOccurredAtDesc(rubricId);
    }

    // a precondition is checked after the PUBLISHED/RETIRED refusal, so by here an existing version is a DRAFT
    private void checkPrecondition(RubricSavePrecondition precondition, RubricVersionPayloadDto payload,
                                   Optional<RubricVersion> existing) {
        if (precondition == null || precondition.isNone()) {
            return;
        }
        if (precondition.mustNotExist() && existing.isPresent()) {
            throw RubricPreconditionFailedException.alreadyExists(payload.getId(), payload.getSemver());
        }
        if (precondition.expectedChecksum() != null) {
            if (existing.isEmpty()) {
                throw RubricPreconditionFailedException.missing(payload.getId(), payload.getSemver());
            }
            if (!precondition.expectedChecksum().equalsIgnoreCase(existing.get().getChecksum())) {
                throw RubricPreconditionFailedException.changed(payload.getId(), payload.getSemver());
            }
        }
    }

    private void recordEvent(String rubricId, String semver, RubricLifecycleAction action,
                             String actor, String checksum) {
        lifecycleEventRepository.save(RubricLifecycleEvent.builder()
                .rubricId(rubricId)
                .semver(semver)
                .action(action)
                .actor(actor)
                .checksum(checksum)
                .build());
    }

    private String writeJson(Object value) {
        if (value == null) return null;
        try {
            return objectMapper.writeValueAsString(value);
        } catch (Exception e) {
            logger.error("Failed to write JSON for {}", value.getClass().getSimpleName(), e);
            // rethrow so registerVersion can't persist a version with missing JSON and an
            // empty checksum; the runtime exception also rolls back the transaction
            throw new IllegalStateException(
                    "Failed to serialize " + value.getClass().getSimpleName() + " to JSON", e);
        }
    }

    private String sha256(String input) {
        if (input == null) return "";
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256").digest(input.getBytes(StandardCharsets.UTF_8));
            return HexFormat.of().formatHex(digest);
        } catch (Exception e) {
            throw new IllegalStateException(e);
        }
    }
}
