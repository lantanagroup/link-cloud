package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.CategoryIssuePage;
import com.lantanagroup.link.validation.services.ResultService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;
import java.util.List;
import org.springframework.http.MediaType;
import static org.hamcrest.Matchers.*;
import com.lantanagroup.link.validation.models.CategoryIssueModel;
import static org.mockito.Mockito.*;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

@WebMvcTest(CategoryIssueController.class)
@AutoConfigureMockMvc(addFilters = false)
class CategoryIssueControllerTest {
    static final String URL = "/api/validation/facilities/f/reports/r/categories/c/issues";
    @Autowired MockMvc mvc;
    @MockBean ResultService service;

    @Test
    void emptySearchReturnsRecordsAndMetadata() throws Exception {
        when(service.getCategoryIssuesPage("f", "r", "c", 1, 25)).thenReturn(
                new CategoryIssuePage(List.of(), new CategoryIssuePage.Metadata(25, 1, 0, 0)));
        mvc.perform(get(URL)).andExpect(status().isOk())
                .andExpect(jsonPath("$.records").isEmpty())
                .andExpect(jsonPath("$.metadata.pageSize").value(25))
                .andExpect(jsonPath("$.metadata.totalCount").value(0))
                .andExpect(jsonPath("$.metadata.pageNumber").value(1))
                .andExpect(jsonPath("$.metadata.totalPages").value(0));
    }

    @Test
    void rejectsInvalidAndUnboundedPages() throws Exception {
        mvc.perform(get(URL).param("pageSize", "101")).andExpect(status().isBadRequest())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
                .andExpect(jsonPath("$.status").value(400))
                .andExpect(jsonPath("$.title").value("Bad Request"))
                .andExpect(jsonPath("$.detail").isNotEmpty())
                .andExpect(jsonPath("$.instance").value(URL));
        mvc.perform(get(URL).param("pageSize", "0")).andExpect(status().isBadRequest());
        mvc.perform(get(URL).param("pageNumber", "0")).andExpect(status().isBadRequest());
        verifyNoInteractions(service);
    }

    @Test
    void populatedAndOutOfRangePagesKeepAccurateMetadata() throws Exception {
        var issue = new CategoryIssueModel("location", "message", "invalid", "expression", "patient", null);
        when(service.getCategoryIssuesPage("f", "r", "c", 2, 10)).thenReturn(
                new CategoryIssuePage(List.of(issue), new CategoryIssuePage.Metadata(10, 2, 11, 2)));
        when(service.getCategoryIssuesPage("f", "r", "c", 3, 10)).thenReturn(
                new CategoryIssuePage(List.of(), new CategoryIssuePage.Metadata(10, 3, 11, 2)));
        mvc.perform(get(URL).param("pageNumber", "2").param("pageSize", "10"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.records[0].message").value("message"))
                .andExpect(jsonPath("$.metadata.pageNumber").value(2))
                .andExpect(jsonPath("$.metadata.totalPages").value(2));
        mvc.perform(get(URL).param("pageNumber", "3").param("pageSize", "10"))
                .andExpect(status().isOk()).andExpect(jsonPath("$.records").isEmpty())
                .andExpect(jsonPath("$.metadata.totalCount").value(11))
                .andExpect(jsonPath("$.metadata.totalPages").value(2));
    }

    @Test
    void malformedAndOverflowingPagesReturnBadRequestProblems() throws Exception {
        mvc.perform(get(URL).param("pageNumber", "invalid"))
                .andExpect(status().isBadRequest())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
                .andExpect(jsonPath("$.status").value(400));
        mvc.perform(get(URL).param("pageNumber", "2147483647"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.detail").value("Requested page offset is too large"));
        verifyNoInteractions(service);
    }

    @Test
    void unexpectedFailuresReturnSafeProblem() throws Exception {
        when(service.getCategoryIssuesPage("f", "r", "c", 1, 25))
                .thenThrow(new RuntimeException("private database connection details"));
        mvc.perform(get(URL)).andExpect(status().isInternalServerError())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
                .andExpect(jsonPath("$.type").isNotEmpty())
                .andExpect(jsonPath("$.title").value("Internal Server Error"))
                .andExpect(jsonPath("$.status").value(500))
                .andExpect(jsonPath("$.detail", not(containsString("private database"))));
    }
}
