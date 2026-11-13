package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.services.categoryoverride.FindingResultAdapter;
import com.lantanagroup.link.validation.records.BridgeOutcome;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.stream.Collectors;

@Service
public class LegacyResultMapper {

    private final CategoryRepository categoryRepository;

    public LegacyResultMapper(CategoryRepository categoryRepository) {
        this.categoryRepository = categoryRepository;
    }

    public BridgeOutcome toResults(ValidationResultEnvelope envelope, String facilityId, String patientId, String reportId) {
        // The envelope's top-level status is set by ResultEnvelopeAssembler to score.getInterpretation(),
        // so read it directly rather than dereferencing getScore() (which can be null on hand-built
        // envelopes). A null status is fine — the consumer falls back to the category rollup.
        RubricResultStatus interpretation = envelope.getStatus();

        List<FindingDto> findings = envelope.getFindings();
        if (findings == null || findings.isEmpty()) {
            return new BridgeOutcome(List.of(), interpretation);
        }

        Map<String, Category> categoriesById = loadCategories(findings);

        List<Result> results = new ArrayList<>(findings.size());
        for (FindingDto finding : findings) {
            Result result = FindingResultAdapter.toResult(
                    finding.getSeverity(), finding.getCode(), finding.getMessage(),
                    finding.getLocation(), finding.getExpression());
            if (result.getCode() == null) {
                result.setCode(OperationOutcome.IssueType.NULL);
            }
            result.setFacilityId(facilityId);
            result.setPatientId(patientId);
            result.setReportId(reportId);
            result.setCategories(toCategories(finding.getCategoryIds(), categoriesById));
            results.add(result);
        }
        return new BridgeOutcome(results, interpretation);
    }

    private Map<String, Category> loadCategories(List<FindingDto> findings) {
        Set<String> categoryIds = findings.stream()
                .map(FindingDto::getCategoryIds)
                .filter(Objects::nonNull)
                .flatMap(List::stream)
                .collect(Collectors.toSet());
        if (categoryIds.isEmpty()) {
            return Map.of();
        }
        return categoryRepository.findAllById(categoryIds).stream()
                .collect(Collectors.toMap(Category::getId, c -> c));
    }

    /** Unknown/stale category ids are dropped rather than failing the mapping. */
    private List<Category> toCategories(List<String> categoryIds, Map<String, Category> categoriesById) {
        if (categoryIds == null || categoryIds.isEmpty()) {
            return List.of();
        }
        return categoryIds.stream()
                .map(categoriesById::get)
                .filter(Objects::nonNull)
                .toList();
    }
}
