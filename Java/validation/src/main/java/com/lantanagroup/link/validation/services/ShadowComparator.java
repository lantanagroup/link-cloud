package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import com.lantanagroup.link.validation.records.ShadowSeverityChangeDto;
import com.lantanagroup.link.validation.repositories.ShadowComparisonResultRepository;
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

    public void compareAndLog(
            String correlationId, UUID requestId, String facilityId, String patientId, String reportId,
            String rubricId, boolean ranNewEngine, List<Result> legacyResults, List<Result> newResults) {
        ResultDiff diff = ResultDiff.between(legacyResults, newResults);

        if (diff.isEmpty()) {
            log.info("SHADOW MATCH report={} corr={}", reportId, correlationId);
        } else {
            log.warn("SHADOW DIFF report={} corr={} : {}", reportId, correlationId, diff.summary());
        }

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
