package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.records.BridgeOutcome;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Collections;
import java.util.List;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class LegacyResultMapperTest {

    private static final String FACILITY_ID = "facility-1";
    private static final String PATIENT_ID = "patient-1";
    private static final String REPORT_ID = "report-1";

    @Mock private CategoryRepository categoryRepository;

    private LegacyResultMapper mapper;

    @BeforeEach
    void setUp() {
        mapper = new LegacyResultMapper(categoryRepository);
    }

    private static FindingDto.FindingDtoBuilder finding() {
        return FindingDto.builder()
                .severity(Severity.ERROR)
                .message("some message")
                .location("Bundle.entry[0]")
                .expression("Bundle.entry[0].resource");
    }

    private static Category category(String id, boolean acceptable) {
        Category category = new Category();
        category.setId(id);
        category.setAcceptable(acceptable);
        return category;
    }

    @Test
    void toResults_emptyFindings_returnsEmptyList() {
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(Collections.emptyList())
                .build();

        BridgeOutcome outcome = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID);

        assertTrue(outcome.results().isEmpty());
        verifyNoInteractions(categoryRepository);
    }

    @Test
    void toResults_nullFindings_returnsEmptyList() {
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder().build();

        BridgeOutcome outcome = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID);

        assertTrue(outcome.results().isEmpty());
    }

    @Test
    void toResults_emptyFindings_stillCarriesEnvelopeStatus() {
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(Collections.emptyList())
                .status(RubricResultStatus.ACCEPTABLE)
                .build();

        BridgeOutcome outcome = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID);

        assertTrue(outcome.results().isEmpty());
        assertEquals(RubricResultStatus.ACCEPTABLE, outcome.status());
    }

    @Test
    void toResults_propagatesEnvelopeStatusIntoOutcome() {
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(finding().build()))
                .status(RubricResultStatus.UNACCEPTABLE)
                .build();

        BridgeOutcome outcome = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID);

        assertEquals(1, outcome.results().size());
        assertEquals(RubricResultStatus.UNACCEPTABLE, outcome.status());
    }

    @Test
    void toResults_nullScoreWithFindings_doesNotNpeAndMapsFindings() {
        // A hand-built envelope has no score; the mapper must not dereference getScore(), and it must
        // still map the findings rather than silently discarding them.
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(finding().build()))
                .build();

        BridgeOutcome outcome = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID);

        assertEquals(1, outcome.results().size());
        assertNull(outcome.status());
    }

    @Test
    void toResults_mapsSeverityMessageLocationExpression() {
        FindingDto findingDto = finding().build();
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(findingDto))
                .build();

        Result result = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID).results().get(0);

        assertEquals(OperationOutcome.IssueSeverity.ERROR, result.getSeverity());
        assertEquals("some message", result.getMessage());
        assertEquals("Bundle.entry[0]", result.getLocation());
        assertEquals("Bundle.entry[0].resource", result.getExpression());
    }

    @Test
    void toResults_setsFacilityPatientReportIds() {
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(finding().build()))
                .build();

        Result result = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID).results().get(0);

        assertEquals(FACILITY_ID, result.getFacilityId());
        assertEquals(PATIENT_ID, result.getPatientId());
        assertEquals(REPORT_ID, result.getReportId());
    }

    @Test
    void toResults_rubricNativeCode_defaultsToIssueTypeNullRatherThanNull() {
        // Not a FHIR IssueType and not one of IssueTypes' known aliases, so IssueTypes.parseOrNull(...)
        // returns null - but Result.code is NOT NULL in the DB, so the mapper must supply a default.
        FindingDto findingDto = finding().code("some-rubric-native-check-code").build();
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(findingDto))
                .build();

        Result result = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID).results().get(0);

        assertEquals(OperationOutcome.IssueType.NULL, result.getCode());
    }

    @Test
    void toResults_knownAliasCode_mapsToItsIssueType() {
        FindingDto findingDto = finding().code("terminology-code-invalid").build();
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(findingDto))
                .build();

        Result result = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID).results().get(0);

        assertEquals(OperationOutcome.IssueType.CODEINVALID, result.getCode());
    }

    @Test
    void toResults_findingWithNoCategoryIds_resultHasEmptyCategories() {
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(finding().build()))
                .build();

        Result result = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID).results().get(0);

        assertNotNull(result.getCategories());
        assertTrue(result.getCategories().isEmpty());
        verifyNoInteractions(categoryRepository);
    }

    @Test
    void toResults_loadsCategoriesById_andDropsUnknownStaleIds() {
        Category known = category("known-cat", true);
        when(categoryRepository.findAllById(Set.of("known-cat", "stale-cat")))
                .thenReturn(List.of(known)); // "stale-cat" no longer exists

        FindingDto findingDto = finding().categoryIds(List.of("known-cat", "stale-cat")).build();
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(findingDto))
                .build();

        Result result = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID).results().get(0);

        assertEquals(List.of(known), result.getCategories());
    }

    @Test
    void toResults_batchLoadsCategoriesOnceAcrossAllFindings() {
        Category shared = category("shared-cat", true);
        when(categoryRepository.findAllById(Set.of("shared-cat"))).thenReturn(List.of(shared));

        FindingDto first = finding().categoryIds(List.of("shared-cat")).build();
        FindingDto second = finding().categoryIds(List.of("shared-cat")).build();
        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .findings(List.of(first, second))
                .build();

        List<Result> results = mapper.toResults(envelope, FACILITY_ID, PATIENT_ID, REPORT_ID).results();

        assertEquals(2, results.size());
        assertEquals(List.of(shared), results.get(0).getCategories());
        assertEquals(List.of(shared), results.get(1).getCategories());
        verify(categoryRepository, times(1)).findAllById(any());
    }
}
