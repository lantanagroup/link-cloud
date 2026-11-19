package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class ShadowFindingsStoreTest {

    @Mock private BlobStorageService blobStorageService;

    private final ObjectMapper objectMapper = new ObjectMapper();

    private ShadowFindingsStore store(String prefix) {
        return new ShadowFindingsStore(Optional.of(blobStorageService), objectMapper, prefix);
    }

    private static ShadowFindingDto finding() {
        return ShadowFindingDto.builder()
                .severity(OperationOutcome.IssueSeverity.ERROR)
                .code(OperationOutcome.IssueType.INVALID)
                .message("naïve – é")
                .location("Patient/1")
                .expression("Patient.gender")
                .categoryIds(List.of("cat-1"))
                .acceptable(false)
                .build();
    }

    @Test
    void store_uploadsUnderThePrefixAndLoadReadsTheSameFindingsBack() {
        ShadowFindingsStore store = store("shadow-compare/");
        ArgumentCaptor<BinaryData> uploaded = ArgumentCaptor.forClass(BinaryData.class);

        String blobName = store.store("facility-1", "report-1", "patient-1", List.of(finding()));

        assertTrue(blobName.matches("shadow-compare/facility-1/report-1/patient-1/[0-9a-f-]{36}\\.json"), blobName);
        verify(blobStorageService).upload(eq(blobName), uploaded.capture());

        when(blobStorageService.download(blobName)).thenReturn(uploaded.getValue());
        List<ShadowFindingDto> loaded = store.load(blobName);

        assertEquals(1, loaded.size());
        ShadowFindingDto f = loaded.get(0);
        assertEquals(OperationOutcome.IssueSeverity.ERROR, f.getSeverity());
        assertEquals(OperationOutcome.IssueType.INVALID, f.getCode());
        assertEquals("naïve – é", f.getMessage());
        assertEquals("Patient/1", f.getLocation());
        assertEquals("Patient.gender", f.getExpression());
        assertEquals(List.of("cat-1"), f.getCategoryIds());
        assertEquals(Boolean.FALSE, f.getAcceptable());
    }

    @Test
    void store_eachCallWritesANewBlob() {
        ShadowFindingsStore store = store("shadow-compare/");

        String first = store.store("f", "r", "p", List.of());
        String second = store.store("f", "r", "p", List.of());

        assertNotEquals(first, second, "a replay must not overwrite a blob an earlier event still points at");
    }

    @Test
    void store_addsTheMissingSlashToThePrefix() {
        String blobName = store("custom").store("f", "r", "p", List.of());

        assertTrue(blobName.startsWith("custom/f/r/p/"), blobName);
    }

    @Test
    void withoutBlobStorage_storeReturnsNullSoTheCallerSendsInline() {
        ShadowFindingsStore store = new ShadowFindingsStore(Optional.empty(), objectMapper, "shadow-compare/");

        assertNull(store.store("f", "r", "p", List.of(finding())));
        assertThrows(IllegalStateException.class, () -> store.load("shadow-compare/x.json"));
        assertDoesNotThrow(() -> store.delete("shadow-compare/x.json"));
    }

    @Test
    void delete_deletesTheBlob() {
        store("shadow-compare/").delete("shadow-compare/x.json");

        verify(blobStorageService).deleteIfExists("shadow-compare/x.json");
    }

    @Test
    void delete_withNoBlobName_doesNothing() {
        store("shadow-compare/").delete(null);

        verifyNoInteractions(blobStorageService);
    }

    @Test
    void delete_failure_isSwallowed() {
        when(blobStorageService.deleteIfExists(any())).thenThrow(new RuntimeException("ABS down"));

        assertDoesNotThrow(() -> store("shadow-compare/").delete("shadow-compare/x.json"));
    }
}
