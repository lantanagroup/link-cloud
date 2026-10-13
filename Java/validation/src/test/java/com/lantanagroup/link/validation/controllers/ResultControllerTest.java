package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.services.ResultService;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;

import java.util.List;

import static org.mockito.Mockito.verify;
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
    void patientIdSummaryIsNotTheCountEndpoint() throws Exception {
        when(resultService.getReportPatientResults(
                "facility-1", "report-1", "summary", OperationOutcome.IssueSeverity.WARNING))
                .thenReturn(List.of());

        mockMvc.perform(get("/api/validation/result/facility-1/report-1/summary")
                        .param("severity", "WARNING"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$").isArray());

        verify(resultService).getReportPatientResults(
                "facility-1", "report-1", "summary", OperationOutcome.IssueSeverity.WARNING);
    }
}
