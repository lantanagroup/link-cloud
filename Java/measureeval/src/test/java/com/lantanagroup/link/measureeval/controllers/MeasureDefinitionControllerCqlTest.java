package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionBundleValidator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import com.lantanagroup.link.measureeval.services.MeasureValidationService;
import org.hl7.fhir.r4.model.Attachment;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import java.nio.charset.StandardCharsets;
import java.util.Optional;

import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

/**
 * The CQL endpoint returns library content that came from an uploaded measure bundle, so it must always
 * answer as plain text, even when a browser asks for HTML.
 */
class MeasureDefinitionControllerCqlTest {
    private static final String CQL_URL = "/api/measureeval/measure-definition/measure-1/TestLibrary/$cql";
    private static final String BROWSER_ACCEPT = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
    private static final String CQL = "library TestLibrary version '1.0.0'\n<script>alert('x')</script>\n";

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        MeasureDefinitionRepository repository = mock(MeasureDefinitionRepository.class);
        when(repository.findById("measure-1")).thenReturn(Optional.of(measureDefinitionWithCql()));

        MeasureDefinitionController controller = new MeasureDefinitionController(repository,
                                                                                 mock(MeasureDefinitionBundleValidator.class),
                                                                                 mock(MeasureEvaluatorCache.class),
                                                                                 mock(MeasureValidationService.class));
        mockMvc = MockMvcBuilders.standaloneSetup(controller).build();
    }

    @Test
    void getMeasureLibraryCql_BrowserAcceptHeader_ReturnsTextPlain() throws Exception {
        mockMvc.perform(get(CQL_URL).header(HttpHeaders.ACCEPT, BROWSER_ACCEPT))
                .andExpect(status().isOk())
                .andExpect(content().contentTypeCompatibleWith(MediaType.TEXT_PLAIN))
                .andExpect(content().string(CQL));
    }

    @Test
    void getMeasureLibraryCql_NoAcceptHeader_ReturnsTextPlain() throws Exception {
        mockMvc.perform(get(CQL_URL))
                .andExpect(status().isOk())
                .andExpect(content().contentTypeCompatibleWith(MediaType.TEXT_PLAIN))
                .andExpect(content().string(CQL));
    }

    private static MeasureDefinition measureDefinitionWithCql() {
        Library library = new Library();
        library.setUrl("http://example.org/fhir/Library/TestLibrary");
        library.addContent(new Attachment()
                .setContentType("text/cql")
                .setData(CQL.getBytes(StandardCharsets.UTF_8)));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(library);

        MeasureDefinition measureDefinition = new MeasureDefinition();
        measureDefinition.setId("measure-1");
        measureDefinition.setBundle(bundle);
        return measureDefinition;
    }
}
