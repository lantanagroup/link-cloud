package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.services.ResultCsvExportService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;
import java.io.Writer;
import static org.mockito.Mockito.*;
import static org.mockito.ArgumentMatchers.*;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

@WebMvcTest(ResultCsvController.class)
@AutoConfigureMockMvc(addFilters = false)
class ResultCsvControllerTest {
    @Autowired MockMvc mvc;
    @MockBean ResultCsvExportService service;
    static final String ID = "37a93980-ffc4-431c-84c6-b87ac14762d8";

    @Test
    void returnsCsvAttachment() throws Exception {
        when(service.upperBound("facility", ID)).thenReturn(null);
        doAnswer(call -> { ((Writer) call.getArgument(3)).write("resultId,patientId\r\n"); return null; })
                .when(service).write(eq("facility"), eq(ID), isNull(), any(Writer.class));
        mvc.perform(get("/api/validation/facilities/facility/reports/" + ID + "/results.csv"))
                .andExpect(status().isOk())
                .andExpect(header().string("Content-Disposition", "attachment; filename=\"validation-results-" + ID + ".csv\""))
                .andExpect(header().string("Cache-Control", "no-store"))
                .andExpect(content().contentTypeCompatibleWith("text/csv"))
                .andExpect(content().string("resultId,patientId\r\n"));
    }
    @Test
    void returnsProblemWithoutAttachmentWhenQueryFailsBeforeStreaming() throws Exception {
        when(service.upperBound("facility", ID))
                .thenThrow(new org.springframework.dao.DataAccessResourceFailureException("Database unavailable"));
        mvc.perform(get("/api/validation/facilities/facility/reports/" + ID + "/results.csv")
                        .accept("text/csv"))
                .andExpect(status().isInternalServerError())
                .andExpect(header().doesNotExist("Content-Disposition"))
                .andExpect(content().contentTypeCompatibleWith("application/problem+json"))
                .andExpect(jsonPath("$.status").value(500))
                .andExpect(jsonPath("$.detail").value("An unexpected error occurred"));
        verify(service, never()).write(anyString(), anyString(), any(), any());
    }

    @Test
    void rejectsInvalidUuidBeforeQuerying() throws Exception {
        mvc.perform(get("/api/validation/facilities/facility/reports/invalid/results.csv")).andExpect(status().isBadRequest());
        verifyNoInteractions(service);
    }
}
