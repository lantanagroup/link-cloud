package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import com.lantanagroup.link.validation.services.ArtifactService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;

import java.util.Set;

import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(ArtifactController.class)
@AutoConfigureMockMvc(addFilters = false)
class ArtifactControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private ArtifactRepository artifactRepository;

    @MockBean
    private ArtifactService artifactService;

    @Test
    void getTerminologyDependencies() throws Exception {
        Set<String> dependencies = Set.of("http://test.com/ValueSet|1.0", "http://test.com/CodeSystem");
        when(artifactService.getTerminologyDependencies()).thenReturn(dependencies);

        mockMvc.perform(get("/api/validation/artifact/terminology-dependencies"))
                .andExpect(status().isOk())
                .andExpect(content().json("[\"http://test.com/ValueSet|1.0\",\"http://test.com/CodeSystem\"]"));
    }
}
