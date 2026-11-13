package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import com.lantanagroup.link.validation.records.ShadowSeverityChangeDto;
import com.lantanagroup.link.validation.repositories.ShadowComparisonResultRepository;
import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.services.shadow.ResultDiff;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

/** ADR-0003 shadow-run: the parallel-run diff -- computes, logs, and persists match/mismatch outcomes. */
@Service
@Slf4j
public class ShadowComparator {

    private final ShadowComparisonResultRepository shadowComparisonResultRepository;
    private final ObjectMapper objectMapper;

    public ShadowComparator(
            ShadowComparisonResultRepository shadowComparisonResultRepository, ObjectMapper objectMapper) {
        this.shadowComparisonResultRepository = shadowComparisonResultRepository;
        this.objectMapper = objectMapper;
    }

    /**
     * Logs whether the two engines reached the same verdict for the patient, and persists the finding-level diff.
     *
     * <p>The verdict is what the log reports: whether each engine would pass the patient decides what is submitted, and
     * that is what the comparison is for while the rubric engine is being tested. The finding diff is kept for detail,
     * but isn't logged as a mismatch: the engines word and locate their findings differently (a rubric's checks are its
     * own, not HAPI's profile validation), so findings rarely pair up even when the verdicts agree.
     *
     * @param legacyValid whether the legacy engine passed the patient; null when unknown
     * @param rubricValid whether the rubric engine passed the patient; null when unknown
     */
    public void compareAndLog(
            String correlationId, UUID requestId, String facilityId, String patientId, String reportId,
            String rubricId, boolean ranNewEngine, Boolean legacyValid, Boolean rubricValid,
            List<Result> legacyResults, List<Result> newResults) {
        ResultDiff diff = ResultDiff.between(legacyResults, newResults);

        String agreement = legacyValid == null || rubricValid == null ? "unknown"
                : legacyValid.equals(rubricValid) ? "agree" : "disagree";
        log.info("Shadow verdict for report {} patient {}: legacy {}, rubric {} ({}); findings: legacy {}, rubric {}",
                LogUtils.sanitize(reportId), LogUtils.sanitize(patientId), verdict(legacyValid), verdict(rubricValid),
                agreement, legacyResults.size(), newResults.size());

        String addedJson = writeFindingsJson(diff.getAdded(), "added", reportId);
        String missingJson = writeFindingsJson(diff.getMissing(), "missing", reportId);
        String severityChangedJson = writeSeverityChangedJson(diff.getSeverityChanged(), reportId);

        OffsetDateTime comparedAt = OffsetDateTime.now();
        ShadowComparisonResult entity = ShadowComparisonResult.builder()
                .requestId(requestId)
                .correlationId(correlationId)
                .facilityId(facilityId)
                .patientId(patientId)
                .reportId(reportId)
                .rubricId(rubricId)
                .ranNewEngine(ranNewEngine)
                .matched(diff.isEmpty())
                .addedCount(diff.getAdded().size())
                .missingCount(diff.getMissing().size())
                .severityChangedCount(diff.getSeverityChanged().size())
                .matchedFindingCount(diff.getMatchedCount())
                .addedJson(addedJson)
                .missingJson(missingJson)
                .requestId(requestId)
                .severityChangedJson(severityChangedJson)
                .comparedAt(comparedAt)
                .build();
        shadowComparisonResultRepository.save(entity);
    }

    private static String verdict(Boolean valid) {
        return valid == null ? "unknown" : valid ? "passed" : "failed";
    }

    private String writeFindingsJson(List<Result> results, String category, String reportId) {
        if (results.isEmpty()) {
            return null;
        }
        try {
            return objectMapper.writeValueAsString(results.stream().map(ShadowFindingDto::from).toList());
        } catch (Exception e) {
            log.warn("Failed to serialize shadow {} findings for report {}", category, reportId, e);
            return null;
        }
    }

    private String writeSeverityChangedJson(List<ResultDiff.SeverityChange> changes, String reportId) {
        if (changes.isEmpty()) {
            return null;
        }
        try {
            return objectMapper.writeValueAsString(changes.stream().map(ShadowSeverityChangeDto::from).toList());
        } catch (Exception e) {
            log.warn("Failed to serialize shadow severity-changed findings for report {}", reportId, e);
            return null;
        }
    }
}
