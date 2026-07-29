package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricNotFoundException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.services.RubricRegistryService;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageImpl;
import org.springframework.data.domain.Pageable;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.RequestBuilder;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;

import static org.hamcrest.Matchers.containsString;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(RubricController.class)
@AutoConfigureMockMvc(addFilters = false)
class RubricControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private RubricRegistryService registry;

    private static final String BASE = "/api/validation/rubrics";

    // A minimal, bean-validation-clean register payload (valid id, semver, one well-formed check).
    private static String validPayload(String id, String semver) {
        return """
                {
                  "id": "%s",
                  "semver": "%s",
                  "title": "Demo Rubric",
                  "owner": "qa",
                  "dimensions": ["CONFORMANCE"],
                  "checks": [
                    {
                      "id": "c1",
                      "type": "FHIRPATH",
                      "dimension": "CONFORMANCE",
                      "parameters": { "expression": "Patient.name.exists()" },
                      "ordinal": 0,
                      "enabled": true
                    }
                  ]
                }
                """.formatted(id, semver);
    }

    private static RubricVersion version(String rubricId, String semver, RubricVersionStatus status) {
        return RubricVersion.builder()
                .rubricVersionId(UUID.randomUUID())
                .rubricId(rubricId)
                .semver(semver)
                .status(status)
                .checksum("abc123")
                .createdBy("qa")
                .createdAt(OffsetDateTime.now())
                .build();
    }

    private static Rubric rubric(String rubricId) {
        return Rubric.builder()
                .rubricId(rubricId)
                .title("Demo Rubric")
                .owner("qa")
                .createdAt(OffsetDateTime.now())
                .updatedAt(OffsetDateTime.now())
                .build();
    }

    /**
     * Assert the observed HTTP status against the documented contract, robust to the two ways an
     * unmapped exception can surface in MockMvc: as a 500 response, or as a rethrow out of perform().
     */
    private void expectStatus(RequestBuilder rb, int expected) throws Exception {
        MvcResult result;
        try {
            result = mockMvc.perform(rb).andReturn();
        } catch (Exception ex) {
            Throwable root = ex;
            while (root.getCause() != null && root.getCause() != root) {
                root = root.getCause();
            }
            throw new AssertionError("Expected HTTP " + expected
                    + " but request handling threw (no exception->status mapping): "
                    + root.getClass().getSimpleName() + ": " + root.getMessage());
        }
        int actual = result.getResponse().getStatus();
        assertEquals(expected, actual,
                "Expected HTTP " + expected + " per documented contract but got " + actual);
    }

    // ---------------------------------------------------------------------------------------------
    // Register a new version
    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("POST /{rubricId}/versions  (register)")
    class Register {

        @Test
        @DisplayName("valid payload -> 201 Created with Location and DRAFT status")
        void register_valid() throws Exception {
            when(registry.registerVersion(any(), any()))
                    .thenReturn(version("piqi.core", "1.0.0", RubricVersionStatus.DRAFT));

            mockMvc.perform(post(BASE + "/piqi.core/versions")
                            .contentType(MediaType.APPLICATION_JSON)
                            .content(validPayload("piqi.core", "1.0.0")))
                    .andExpect(status().isCreated())
                    .andExpect(header().string("Location", containsString("/piqi.core/versions/1.0.0")))
                    .andExpect(jsonPath("$.data.status").value("DRAFT"))
                    .andExpect(jsonPath("$.data.semver").value("1.0.0"));
        }

        @Test
        @DisplayName("blank id -> 400 (bean validation)")
        void register_blankId() throws Exception {
            expectStatus(post(BASE + "/piqi.core/versions")
                    .contentType(MediaType.APPLICATION_JSON)
                    .content(validPayload("", "1.0.0")), 400);
        }

        @Test
        @DisplayName("non-semver version -> 400 (bean validation)")
        void register_badSemver() throws Exception {
            // Service stubbed to succeed: if @Pattern on semver were enforced the request would be
            // rejected with 400 *before* the service is ever called. If validation is inert it slips
            // through to a 201, which is the defect this asserts against.
            when(registry.registerVersion(any(), any()))
                    .thenReturn(version("piqi.core", "1.0.0", RubricVersionStatus.DRAFT));
            expectStatus(post(BASE + "/piqi.core/versions")
                    .contentType(MediaType.APPLICATION_JSON)
                    .content(validPayload("piqi.core", "v1")), 400);
        }

        @Test
        @DisplayName("empty checks list -> 400 (bean validation)")
        void register_emptyChecks() throws Exception {
            when(registry.registerVersion(any(), any()))
                    .thenReturn(version("piqi.core", "1.0.0", RubricVersionStatus.DRAFT));
            String body = """
                    { "id": "piqi.core", "semver": "1.0.0", "checks": [] }
                    """;
            expectStatus(post(BASE + "/piqi.core/versions")
                    .contentType(MediaType.APPLICATION_JSON)
                    .content(body), 400);
        }

        @Test
        @DisplayName("path id vs body id mismatch -> 400 (documented)")
        void register_idMismatch() throws Exception {
            expectStatus(post(BASE + "/piqi.core/versions")
                    .contentType(MediaType.APPLICATION_JSON)
                    .content(validPayload("something.else", "1.0.0")), 400);
        }

        @Test
        @DisplayName("duplicate semver -> 409 (documented)")
        void register_duplicateSemver() throws Exception {
            when(registry.registerVersion(any(), any()))
                    .thenThrow(new RubricVersionConflictException("piqi.core", "1.0.0"));
            expectStatus(post(BASE + "/piqi.core/versions")
                    .contentType(MediaType.APPLICATION_JSON)
                    .content(validPayload("piqi.core", "1.0.0")), 409);
        }

        @Test
        @DisplayName("semantically invalid definition -> 400 (documented)")
        void register_invalidDefinition() throws Exception {
            when(registry.registerVersion(any(), any()))
                    .thenThrow(new InvalidRubricDefinitionException("Invalid rubric definition",
                            List.of("checks[c1]: FHIRPATH requires parameters.expression")));
            expectStatus(post(BASE + "/piqi.core/versions")
                    .contentType(MediaType.APPLICATION_JSON)
                    .content(validPayload("piqi.core", "1.0.0")), 400);
        }

        @Test
        @DisplayName("malformed JSON body -> 400")
        void register_malformedJson() throws Exception {
            expectStatus(post(BASE + "/piqi.core/versions")
                    .contentType(MediaType.APPLICATION_JSON)
                    .content("{ not json "), 400);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Publish
    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("POST /{rubricId}/versions/{semver}/$publish")
    class Publish {

        @Test
        @DisplayName("draft -> 200 PUBLISHED")
        void publish_valid() throws Exception {
            when(registry.publish(eq("piqi.core"), eq("1.0.0"), any()))
                    .thenReturn(version("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED));
            mockMvc.perform(post(BASE + "/piqi.core/versions/1.0.0/$publish"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.status").value("PUBLISHED"));
        }

        @Test
        @DisplayName("unknown version -> 404 (documented)")
        void publish_notFound() throws Exception {
            when(registry.publish(any(), any(), any()))
                    .thenThrow(new RubricVersionNotFoundException("piqi.core", "9.9.9"));
            expectStatus(post(BASE + "/piqi.core/versions/9.9.9/$publish"), 404);
        }

        @Test
        @DisplayName("already published/retired -> 409 (documented)")
        void publish_wrongState() throws Exception {
            when(registry.publish(any(), any(), any()))
                    .thenThrow(new RubricLifecycleException("piqi.core", "1.0.0",
                            RubricVersionStatus.PUBLISHED, "publish"));
            expectStatus(post(BASE + "/piqi.core/versions/1.0.0/$publish"), 409);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Retire
    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("POST /{rubricId}/versions/{semver}/$retire")
    class Retire {

        @Test
        @DisplayName("published -> 200 RETIRED")
        void retire_valid() throws Exception {
            when(registry.retire(eq("piqi.core"), eq("1.0.0"), any()))
                    .thenReturn(version("piqi.core", "1.0.0", RubricVersionStatus.RETIRED));
            mockMvc.perform(post(BASE + "/piqi.core/versions/1.0.0/$retire"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.status").value("RETIRED"));
        }

        @Test
        @DisplayName("unknown version -> 404 (documented)")
        void retire_notFound() throws Exception {
            when(registry.retire(any(), any(), any()))
                    .thenThrow(new RubricVersionNotFoundException("piqi.core", "9.9.9"));
            expectStatus(post(BASE + "/piqi.core/versions/9.9.9/$retire"), 404);
        }

        @Test
        @DisplayName("still a draft -> 409 (documented)")
        void retire_wrongState() throws Exception {
            when(registry.retire(any(), any(), any()))
                    .thenThrow(new RubricLifecycleException("piqi.core", "1.0.0",
                            RubricVersionStatus.DRAFT, "retire"));
            expectStatus(post(BASE + "/piqi.core/versions/1.0.0/$retire"), 409);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("GET reads")
    class Reads {

        @Test
        @DisplayName("list rubrics -> 200 paged envelope")
        void listRubrics() throws Exception {
            Page<Rubric> page = new PageImpl<>(List.of(rubric("piqi.core")), Pageable.ofSize(20), 1);
            when(registry.listRubrics(any())).thenReturn(page);
            when(registry.versionsByRubricId(any()))
                    .thenReturn(Map.of("piqi.core", List.of(version("piqi.core", "1.0.0", RubricVersionStatus.DRAFT))));

            mockMvc.perform(get(BASE))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.content[0].rubricId").value("piqi.core"));
        }

        @Test
        @DisplayName("get rubric -> 200")
        void getRubric() throws Exception {
            when(registry.getRubric("piqi.core")).thenReturn(rubric("piqi.core"));
            when(registry.latestPublishedSemver("piqi.core")).thenReturn(Optional.of("1.0.0"));
            when(registry.listVersions("piqi.core", null))
                    .thenReturn(List.of(version("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED)));

            mockMvc.perform(get(BASE + "/piqi.core"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.rubricId").value("piqi.core"));
        }

        @Test
        @DisplayName("get unknown rubric -> 404 (documented)")
        void getRubric_notFound() throws Exception {
            when(registry.getRubric("nope")).thenThrow(new RubricNotFoundException("nope"));
            expectStatus(get(BASE + "/nope"), 404);
        }

        @Test
        @DisplayName("list versions (filtered) -> 200")
        void listVersions() throws Exception {
            when(registry.listVersions(eq("piqi.core"), any()))
                    .thenReturn(List.of(version("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED)));
            mockMvc.perform(get(BASE + "/piqi.core/versions").param("status", "PUBLISHED"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data[0].semver").value("1.0.0"));
        }

        @Test
        @DisplayName("list versions reports each version's own state (DRAFT/PUBLISHED/RETIRED)")
        void listVersions_carriesStatusPerVersion() throws Exception {
            // Service returns newest-first, mixed states.
            when(registry.listVersions(eq("piqi.core"), isNull())).thenReturn(List.of(
                    version("piqi.core", "2.0.0", RubricVersionStatus.DRAFT),
                    version("piqi.core", "1.1.0", RubricVersionStatus.PUBLISHED),
                    version("piqi.core", "1.0.0", RubricVersionStatus.RETIRED)
            ));
            mockMvc.perform(get(BASE + "/piqi.core/versions"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data[0].semver").value("2.0.0"))
                    .andExpect(jsonPath("$.data[0].status").value("DRAFT"))
                    .andExpect(jsonPath("$.data[1].semver").value("1.1.0"))
                    .andExpect(jsonPath("$.data[1].status").value("PUBLISHED"))
                    .andExpect(jsonPath("$.data[2].semver").value("1.0.0"))
                    .andExpect(jsonPath("$.data[2].status").value("RETIRED"));
        }

        @Test
        @DisplayName("list versions for unknown rubric -> 404 (documented)")
        void listVersions_notFound() throws Exception {
            when(registry.listVersions(eq("nope"), isNull()))
                    .thenThrow(new RubricNotFoundException("nope"));
            expectStatus(get(BASE + "/nope/versions"), 404);
        }

        @Test
        @DisplayName("get version -> 200")
        void getVersion() throws Exception {
            RubricVersion v = version("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
            when(registry.getVersion("piqi.core", "1.0.0")).thenReturn(v);
            when(registry.getChecks(any())).thenReturn(List.of());
            mockMvc.perform(get(BASE + "/piqi.core/versions/1.0.0"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.semver").value("1.0.0"));
        }

        @Test
        @DisplayName("get unknown version -> 404 (documented)")
        void getVersion_notFound() throws Exception {
            when(registry.getVersion("piqi.core", "9.9.9"))
                    .thenThrow(new RubricVersionNotFoundException("piqi.core", "9.9.9"));
            expectStatus(get(BASE + "/piqi.core/versions/9.9.9"), 404);
        }

        @Test
        @DisplayName("lifecycle events -> 200")
        void listEvents() throws Exception {
            when(registry.listLifecycleEvents("piqi.core")).thenReturn(List.of());
            mockMvc.perform(get(BASE + "/piqi.core/events"))
                    .andExpect(status().isOk());
        }

        @Test
        @DisplayName("lifecycle events for unknown rubric -> 404 (documented)")
        void listEvents_notFound() throws Exception {
            when(registry.listLifecycleEvents("nope")).thenThrow(new RubricNotFoundException("nope"));
            expectStatus(get(BASE + "/nope/events"), 404);
        }
    }
}
