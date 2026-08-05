package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.models.RubricResultDto;
import com.lantanagroup.link.validation.services.RubricResultQueryService;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;

import java.util.Optional;
import java.util.UUID;

import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(VaasResultController.class)
@AutoConfigureMockMvc(addFilters = false)
class VaasResultControllerTest {

    private static final String BASE = "/api/validation/requests";

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private RubricResultQueryService resultQueryService;

    @Test
    @DisplayName("GET a known request id returns 200 with the mapped result DTO")
    void returnsPersistedResult() throws Exception {
        UUID requestId = UUID.randomUUID();
        RubricResultDto dto = RubricResultDto.builder()
                .requestId(requestId)
                .resultId(UUID.randomUUID())
                .rubricId("piqi.core")
                .status(RubricResultStatus.ACCEPTABLE)
                .build();
        when(resultQueryService.findByRequestId(requestId)).thenReturn(Optional.of(dto));

        mockMvc.perform(get(BASE + "/" + requestId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.rubricId").value("piqi.core"))
                .andExpect(jsonPath("$.status").value("ACCEPTABLE"));
    }

    @Test
    @DisplayName("GET an unknown request id returns 404")
    void returnsNotFoundForUnknownId() throws Exception {
        UUID requestId = UUID.randomUUID();
        when(resultQueryService.findByRequestId(requestId)).thenReturn(Optional.empty());

        mockMvc.perform(get(BASE + "/" + requestId))
                .andExpect(status().isNotFound());
    }
}
