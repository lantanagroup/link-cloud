package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricNotFoundException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.CheckDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.models.Semver;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricLifecycleEventRepository;
import com.lantanagroup.link.validation.repositories.RubricRepository;
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
import java.util.HexFormat;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
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

    @Transactional
    public RubricVersion registerVersion(RubricVersionPayloadDto payload, String actor) {
        definitionValidator.validate(payload);

        String definitionJson = writeJson(payload);
        String checksum = sha256(definitionJson);

        // a semver may be registered exactly once — re-registration conflicts even for
        if (rubricVersionRepository.findByRubricIdAndSemver(payload.getId(), payload.getSemver()).isPresent()) {
            throw new RubricVersionConflictException(payload.getId(), payload.getSemver());
        }

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

        RubricVersion version = RubricVersion.builder()
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
        recordEvent(rubric.getRubricId(), payload.getSemver(), RubricLifecycleAction.REGISTERED, actor, checksum);
        logger.info("Registered rubric {} v{} with {} checks (status=DRAFT)",
                LogUtils.sanitize(rubric.getRubricId()), LogUtils.sanitize(payload.getSemver()), checks.size());
        return version;
    }

    @Transactional
    public RubricVersion publish(String rubricId, String semver, String publishedBy) {
        RubricVersion v = rubricVersionRepository.findByRubricIdAndSemver(rubricId, semver)
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, semver));
        if (v.getStatus() != RubricVersionStatus.DRAFT) {
            throw new RubricLifecycleException(rubricId, semver, v.getStatus(), "publish");
        }
        v.setStatus(RubricVersionStatus.PUBLISHED);
        v.setPublishedAt(OffsetDateTime.now());
        v.setPublishedBy(publishedBy);
        v = rubricVersionRepository.save(v);
        recordEvent(rubricId, semver, RubricLifecycleAction.PUBLISHED, publishedBy, v.getChecksum());
        logger.info("Published rubric {} v{} by {}",
                LogUtils.sanitize(rubricId), LogUtils.sanitize(semver), LogUtils.sanitize(publishedBy));
        return v;
    }

    @Transactional
    public RubricVersion retire(String rubricId, String semver, String retiredBy) {
        RubricVersion v = rubricVersionRepository.findByRubricIdAndSemver(rubricId, semver)
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, semver));
        if (v.getStatus() != RubricVersionStatus.PUBLISHED) {
            throw new RubricLifecycleException(rubricId, semver, v.getStatus(), "retire");
        }
        v.setStatus(RubricVersionStatus.RETIRED);
        v.setRetiredAt(OffsetDateTime.now());
        v.setRetiredBy(retiredBy);
        v = rubricVersionRepository.save(v);
        recordEvent(rubricId, semver, RubricLifecycleAction.RETIRED, retiredBy, v.getChecksum());
        logger.info("Retired rubric {} v{} by {}",
                LogUtils.sanitize(rubricId), LogUtils.sanitize(semver), LogUtils.sanitize(retiredBy));
        return v;
    }

    public Page<Rubric> listRubrics(Pageable pageable) {
        return rubricRepository.findAll(pageable);
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
    // Used by the rubric list endpoint to avoid an N+1 query.
    public Map<String, List<RubricVersion>> versionsByRubricId(Collection<String> rubricIds) {
        if (rubricIds.isEmpty()) return Map.of();
        Map<String, List<RubricVersion>> grouped = rubricVersionRepository.findByRubricIdIn(rubricIds).stream()
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
        return rubricVersionRepository.findByRubricIdAndSemver(rubricId, semver)
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, semver));
    }

    public Optional<RubricVersion> findVersion(String rubricId, String semver) {
        return rubricVersionRepository.findByRubricIdAndSemver(rubricId, semver);
    }

    public List<RubricCheck> getChecks(UUID rubricVersionId) {
        return rubricCheckRepository.findByRubricVersionIdOrderByOrdinalAsc(rubricVersionId);
    }

    public List<RubricLifecycleEvent> listLifecycleEvents(String rubricId) {
        if (!rubricRepository.existsById(rubricId)) {
            throw new RubricNotFoundException(rubricId);
        }
        return lifecycleEventRepository.findByRubricIdOrderByOccurredAtDesc(rubricId);
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
