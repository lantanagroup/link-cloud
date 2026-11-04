package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.CategoryCountModel;
import com.lantanagroup.link.validation.models.ReportCategoryCountsModel;
import com.lantanagroup.link.validation.services.CategoryCountService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(CategoryCountController.class)
@AutoConfigureMockMvc(addFilters = false)
class CategoryCountControllerTest {
    private static final String URL = "/api/validation/facilities/facility-1/reports/report-1/category-counts";

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private CategoryCountService categoryCountService;

    @Test
    void getReportCategoryCounts_returnsTheStoredCounts() throws Exception {
        when(categoryCountService.getReportCounts("facility-1", "report-1", null))
                .thenReturn(Optional.of(new ReportCategoryCountsModel(
                        "facility-1",
                        "report-1",
                        OffsetDateTime.parse("2026-10-21T12:00:00Z"),
                        List.of(new CategoryCountModel("category-a", "Category A", false, true, 7)))));

        mockMvc.perform(get(URL))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.facilityId").value("facility-1"))
                .andExpect(jsonPath("$.reportId").value("report-1"))
                .andExpect(jsonPath("$.lastUpdated").value("2026-10-21T12:00:00Z"))
                .andExpect(jsonPath("$.categories[0].categoryId").value("category-a"))
                .andExpect(jsonPath("$.categories[0].title").value("Category A"))
                .andExpect(jsonPath("$.categories[0].acceptable").value(false))
                .andExpect(jsonPath("$.categories[0].submit").value(true))
                .andExpect(jsonPath("$.categories[0].issues").value(7));
    }

    @Test
    void getReportCategoryCounts_passesTheSubmitFilter() throws Exception {
        when(categoryCountService.getReportCounts("facility-1", "report-1", false))
                .thenReturn(Optional.of(new ReportCategoryCountsModel(
                        "facility-1",
                        "report-1",
                        OffsetDateTime.now(),
                        List.of())));

        mockMvc.perform(get(URL).param("submit", "false"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.categories").isEmpty());

        verify(categoryCountService).getReportCounts("facility-1", "report-1", false);
    }

    @Test
    void getReportCategoryCounts_reportWithNoStoredCounts_returnsNotFound() throws Exception {
        when(categoryCountService.getReportCounts("facility-1", "report-1", null)).thenReturn(Optional.empty());

        mockMvc.perform(get(URL))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.detail").value("Validation counts are unavailable for this report"));
    }

    @Test
    void getReportCategoryCounts_invalidSubmitValue_returnsBadRequest() throws Exception {
        mockMvc.perform(get(URL).param("submit", "sometimes"))
                .andExpect(status().isBadRequest());

        verify(categoryCountService, never()).getReportCounts(anyString(), anyString(), any());
    }
}
