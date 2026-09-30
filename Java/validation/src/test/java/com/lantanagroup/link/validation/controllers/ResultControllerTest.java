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

import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(ResultController.class)
@AutoConfigureMockMvc(addFilters = false)
class ResultControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private ResultService resultService;

    @Test
    void summarizeReportResults_isNotCapturedAsAPatientId() throws Exception {
        when(resultService.summarizeReportResults(
                "facility-1", "report-1", OperationOutcome.IssueSeverity.WARNING))
                .thenReturn(new ResultSummaryModel(12L, "WARNING"));

        mockMvc.perform(get("/api/validation/result/facility-1/report-1/summary")
                        .param("severity", "WARNING"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.count").value(12))
                .andExpect(jsonPath("$.severity").value("WARNING"));
    }
}
