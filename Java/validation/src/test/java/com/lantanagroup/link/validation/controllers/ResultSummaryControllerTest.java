package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.ResultSummaryModel;
import com.lantanagroup.link.validation.services.ResultService;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(ResultSummaryController.class)
@AutoConfigureMockMvc(addFilters = false)
class ResultSummaryControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private ResultService resultService;

    @Test
    void summarizeReportResults_returnsTheCount() throws Exception {
        when(resultService.summarizeReportResults(
                "facility-1", "report-1", OperationOutcome.IssueSeverity.WARNING))
                .thenReturn(new ResultSummaryModel(12L, "WARNING"));

        mockMvc.perform(get("/api/validation/result-summaries/facility-1/report-1")
                        .param("severity", "WARNING"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.count").value(12))
                .andExpect(jsonPath("$.severity").value("WARNING"));
    }

    @Test
    void summarizeReportResults_rejectsNullSeverity() throws Exception {
        mockMvc.perform(get("/api/validation/result-summaries/facility-1/report-1")
                        .param("severity", "NULL"))
                .andExpect(status().isBadRequest());

        verify(resultService, never()).summarizeReportResults(anyString(), anyString(), any());
    }
}
