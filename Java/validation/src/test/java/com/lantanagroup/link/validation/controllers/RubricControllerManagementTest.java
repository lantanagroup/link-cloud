package com.lantanagroup.link.validation.controllers;

import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.dataformat.yaml.YAMLMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricPreconditionFailedException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.RubricSettingsDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.records.RubricSavePrecondition;
import com.lantanagroup.link.validation.records.RubricSearch;
import com.lantanagroup.link.validation.services.RubricRegistryService;
import com.lantanagroup.link.validation.services.RubricRegistryService.DefinitionPreview;
import com.lantanagroup.link.validation.services.RubricSettingsService;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageImpl;
import org.springframework.data.domain.Pageable;
import org.springframework.data.domain.Sort;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.Optional;

import static org.hamcrest.Matchers.containsString;
import static org.hamcrest.Matchers.hasSize;
import static org.hamcrest.Matchers.startsWith;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

/**
 * The endpoints the rubric management interface needs beyond register, publish and retire: searching the
 * list, checking a definition without saving it, exporting one, the settings, saves that refuse to replace
 * unseen work, and deleting a draft. {@link RubricControllerTest} covers the rest of the controller.
 */
@WebMvcTest(RubricController.class)
@AutoConfigureMockMvc(addFilters = false)
class RubricControllerManagementTest {

    private static final String BASE = "/api/validation/rubrics";
    private static final MediaType YAML = MediaType.parseMediaType("application/yaml");

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private RubricRegistryService registry;

    @MockBean
    private RubricSettingsService settings;

    private static String validJson(String id, String semver) {
        return """
                {
                  "id": "%s",
                  "semver": "%s",
                  "title": "Demo Rubric",
                  "owner": "qa",
                  "dimensions": ["CONFORMANCE"],
                  "scoringPolicy": { "type": "piqi-dimension-scorecard", "rollup": "worst-of" },
                  "checks": [
                    {
                      "id": "c1",
                      "type": "FHIRPATH",
                      "dimension": "CONFORMANCE",
                      "severityOverride": "ERROR",
                      "parameters": { "expression": "Patient.name.exists()", "code": "001" },
                      "ordinal": 0,
                      "enabled": true
                    }
                  ]
                }
                """.formatted(id, semver);
    }

    private static final String VALID_YAML = """
            id: piqi.core
            semver: 1.0.0
            title: Demo Rubric
            dimensions:
              - CONFORMANCE
            scoringPolicy:
              type: piqi-pass-fail
            checks:
              - id: c1
                type: FHIRPATH
                dimension: CONFORMANCE
                severityOverride: ERROR
                parameters:
                  expression: Patient.name.exists()
            """;

    private static RubricVersion version(long id, String rubricId, String semver, RubricVersionStatus status) {
        return RubricVersion.builder()
                .rubricVersionId(id)
                .rubricId(rubricId)
                .semver(semver)
                .status(status)
                .checksum("abc123")
                .createdBy("qa")
                .createdAt(OffsetDateTime.now())
                .build();
    }

    private static Rubric rubric(String rubricId) {
        return Rubric.builder().rubricId(rubricId).title("Demo Rubric").owner("qa")
                .createdAt(OffsetDateTime.now()).updatedAt(OffsetDateTime.now()).build();
    }

    private Page<Rubric> onePage() {
        return new PageImpl<>(List.of(rubric("piqi.core")), Pageable.ofSize(20), 1);
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("GET /  (search, filter, sort)")
    class ListRubrics {

        @Test
        @DisplayName("search and owner are passed to the registry, trimmed")
        void passesSearchAndOwner() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(onePage());

            mockMvc.perform(get(BASE).param("search", "  core ").param("owner", "qa").param("status", "DRAFT"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.content[0].rubricId").value("piqi.core"));

            verify(registry).searchRubrics(eq(new RubricSearch(RubricVersionStatus.DRAFT, "core", "qa")), any());
        }

        @Test
        @DisplayName("a blank search or owner is no filter")
        void blankIsNoFilter() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(onePage());

            mockMvc.perform(get(BASE).param("search", " ").param("owner", "")).andExpect(status().isOk());

            verify(registry).searchRubrics(eq(RubricSearch.ANY), any());
        }

        @Test
        @DisplayName("sorts by rubric id by default, and always ends the sort on the id")
        void sortEndsOnId() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(onePage());
            ArgumentCaptor<Pageable> pageable = ArgumentCaptor.forClass(Pageable.class);

            mockMvc.perform(get(BASE).param("sort", "updatedAt,desc").param("page", "2").param("size", "5"))
                    .andExpect(status().isOk());

            verify(registry).searchRubrics(any(), pageable.capture());
            assertEquals(2, pageable.getValue().getPageNumber());
            assertEquals(5, pageable.getValue().getPageSize());
            assertEquals(Sort.by(Sort.Order.desc("updatedAt"), Sort.Order.asc("rubricId")), pageable.getValue().getSort());
        }

        @Test
        @DisplayName("a sort that already names the id is left as it is: SQL Server refuses a column twice in ORDER BY")
        void sortOnIdIsNotRepeated() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(onePage());
            ArgumentCaptor<Pageable> pageable = ArgumentCaptor.forClass(Pageable.class);

            mockMvc.perform(get(BASE)).andExpect(status().isOk()); // the default sort
            mockMvc.perform(get(BASE).param("sort", "rubricId,desc")).andExpect(status().isOk());
            mockMvc.perform(get(BASE).param("sort", "owner,asc").param("sort", "rubricId,desc")).andExpect(status().isOk());

            verify(registry, org.mockito.Mockito.times(3)).searchRubrics(any(), pageable.capture());
            assertEquals(Sort.by(Sort.Order.asc("rubricId")), pageable.getAllValues().get(0).getSort());
            assertEquals(Sort.by(Sort.Order.desc("rubricId")), pageable.getAllValues().get(1).getSort());
            assertEquals(Sort.by(Sort.Order.asc("owner"), Sort.Order.desc("rubricId")), pageable.getAllValues().get(2).getSort());
        }

        @Test
        @DisplayName("a sort on a property the list does not offer -> 400, and no query is run")
        void refusesUnknownSort() throws Exception {
            mockMvc.perform(get(BASE).param("sort", "checksum,asc"))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.message").value(containsString("Cannot sort by 'checksum'")));

            verify(registry, never()).searchRubrics(any(), any());
        }

        @Test
        @DisplayName("with a status the versions are trimmed to it")
        void statusTrimsVersions() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(onePage());
            when(registry.versionsByRubricId(any(), any())).thenReturn(Map.of());

            mockMvc.perform(get(BASE).param("status", "DRAFT")).andExpect(status().isOk());

            verify(registry).versionsByRubricId(any(), eq(RubricVersionStatus.DRAFT));
        }

        @Test
        @DisplayName("allVersions=true keeps every version of the rubrics the status matched")
        void allVersionsKeepsEveryVersion() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(onePage());
            when(registry.versionsByRubricId(any(), any())).thenReturn(Map.of());

            mockMvc.perform(get(BASE).param("status", "DRAFT").param("allVersions", "true")).andExpect(status().isOk());

            verify(registry).searchRubrics(eq(new RubricSearch(RubricVersionStatus.DRAFT, null, null)), any());
            verify(registry).versionsByRubricId(any(), isNull());
        }

        @Test
        @DisplayName("each version carries its check count; a version with no counted checks has 0")
        void versionsCarryCheckCounts() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(onePage());
            when(registry.versionsByRubricId(any(), any())).thenReturn(Map.of("piqi.core", List.of(
                    version(7L, "piqi.core", "2.0.0", RubricVersionStatus.DRAFT),
                    version(8L, "piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED))));
            when(registry.checkCounts(List.of(7L, 8L))).thenReturn(Map.of(7L, 97));

            mockMvc.perform(get(BASE))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.content[0].versions[0].checkCount").value(97))
                    .andExpect(jsonPath("$.data.content[0].versions[1].checkCount").value(0));
        }

        @Test
        @DisplayName("nothing matching -> 200 with an empty page")
        void emptyIsStillOk() throws Exception {
            when(registry.searchRubrics(any(), any())).thenReturn(Page.empty(Pageable.ofSize(20)));

            mockMvc.perform(get(BASE).param("search", "nothing"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.content", hasSize(0)))
                    .andExpect(jsonPath("$.data.totalElements").value(0));
        }
    }

    @Test
    @DisplayName("GET /{rubricId} and /{rubricId}/versions carry check counts")
    void readsCarryCheckCounts() throws Exception {
        List<RubricVersion> versions = List.of(version(7L, "piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED));
        when(registry.getRubric("piqi.core")).thenReturn(rubric("piqi.core"));
        when(registry.latestPublishedSemver("piqi.core")).thenReturn(Optional.of("1.0.0"));
        when(registry.listVersions("piqi.core", null)).thenReturn(versions);
        when(registry.checkCounts(List.of(7L))).thenReturn(Map.of(7L, 4));

        mockMvc.perform(get(BASE + "/piqi.core"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.data.versions[0].checkCount").value(4));
        mockMvc.perform(get(BASE + "/piqi.core/versions"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.data[0].checkCount").value(4));
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("GET /$owners and /$settings")
    class OwnersAndSettings {

        @Test
        @DisplayName("$owners -> 200 with the owners, not read as a rubric id")
        void owners() throws Exception {
            when(registry.listOwners()).thenReturn(List.of("lantana", "lcg"));

            mockMvc.perform(get(BASE + "/$owners"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data[0]").value("lantana"))
                    .andExpect(jsonPath("$.data[1]").value("lcg"));

            verify(registry, never()).getRubric(any());
        }

        @Test
        @DisplayName("$settings -> 200 with the settings, not read as a rubric id")
        void settingsRead() throws Exception {
            when(settings.getSettings()).thenReturn(RubricSettingsDto.builder()
                    .dryRunRequiredForPublish(true)
                    .pipelineRubricId("measure-report-submission-v1")
                    .maxChecks(200)
                    .scoringPolicies(List.of(new RubricSettingsDto.ScoringPolicySetting("piqi-pass-fail", false)))
                    .build());

            mockMvc.perform(get(BASE + "/$settings"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.dryRunRequiredForPublish").value(true))
                    .andExpect(jsonPath("$.data.pipelineRubricId").value("measure-report-submission-v1"))
                    .andExpect(jsonPath("$.data.maxChecks").value(200))
                    .andExpect(jsonPath("$.data.scoringPolicies[0].type").value("piqi-pass-fail"))
                    .andExpect(jsonPath("$.data.scoringPolicies[0].rollupRequired").value(false));

            verify(registry, never()).getRubric(any());
        }
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("POST /$validate")
    class Validate {

        @Test
        @DisplayName("a valid definition -> 200, valid, with its checksum, and nothing is saved")
        void valid() throws Exception {
            when(registry.preview(any())).thenReturn(new DefinitionPreview("sum-1", null, false));

            mockMvc.perform(post(BASE + "/$validate").contentType(MediaType.APPLICATION_JSON)
                            .content(validJson("piqi.core", "1.0.0")))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.message").value("Rubric definition is valid"))
                    .andExpect(jsonPath("$.data.valid").value(true))
                    .andExpect(jsonPath("$.data.errors", hasSize(0)))
                    .andExpect(jsonPath("$.data.checksum").value("sum-1"))
                    .andExpect(jsonPath("$.data.rubricExists").value(false))
                    .andExpect(jsonPath("$.data.existingStatus").doesNotExist())
                    .andExpect(jsonPath("$.data.definition.id").value("piqi.core"))
                    .andExpect(jsonPath("$.data.definition.checks[0].parameters.expression").value("Patient.name.exists()"));

            verify(registry, never()).registerVersion(any(), any());
            verify(registry, never()).registerVersion(any(), any(), any());
        }

        @Test
        @DisplayName("YAML in -> the definition comes back as JSON")
        void yamlComesBackAsJson() throws Exception {
            when(registry.preview(any())).thenReturn(new DefinitionPreview("sum-1", null, false));

            mockMvc.perform(post(BASE + "/$validate").contentType(YAML).content(VALID_YAML))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.valid").value(true))
                    .andExpect(jsonPath("$.data.definition.scoringPolicy.type").value("piqi-pass-fail"))
                    .andExpect(jsonPath("$.data.definition.checks[0].id").value("c1"));
        }

        @Test
        @DisplayName("reports the version already registered under the id and semver")
        void reportsExisting() throws Exception {
            RubricVersion existing = version(1L, "piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
            when(registry.preview(any())).thenReturn(new DefinitionPreview("sum-1", existing, true));

            mockMvc.perform(post(BASE + "/$validate").contentType(MediaType.APPLICATION_JSON)
                            .content(validJson("piqi.core", "1.0.0")))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.rubricExists").value(true))
                    .andExpect(jsonPath("$.data.existingStatus").value("PUBLISHED"))
                    .andExpect(jsonPath("$.data.existingChecksum").value("abc123"));
        }

        @Test
        @DisplayName("a definition that breaks the rules -> 200, not valid, with every problem and the definition")
        void invalidDefinition() throws Exception {
            when(registry.preview(any())).thenThrow(new InvalidRubricDefinitionException("Invalid rubric definition",
                    List.of("checks[c1]: FHIRPath expression does not parse", "dimensions: duplicate CONFORMANCE")));
            when(registry.rubricExists("piqi.core")).thenReturn(true);
            when(registry.existingVersion(any()))
                    .thenReturn(Optional.of(version(1L, "piqi.core", "1.0.0", RubricVersionStatus.DRAFT)));

            mockMvc.perform(post(BASE + "/$validate").contentType(MediaType.APPLICATION_JSON)
                            .content(validJson("piqi.core", "1.0.0")))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.message").value("Rubric definition is not valid"))
                    .andExpect(jsonPath("$.data.valid").value(false))
                    .andExpect(jsonPath("$.data.errors", hasSize(2)))
                    .andExpect(jsonPath("$.data.errors[0]").value("checks[c1]: FHIRPath expression does not parse"))
                    .andExpect(jsonPath("$.data.checksum").doesNotExist())
                    .andExpect(jsonPath("$.data.definition.id").value("piqi.core"))
                    .andExpect(jsonPath("$.data.rubricExists").value(true))
                    .andExpect(jsonPath("$.data.existingStatus").value("DRAFT"));
        }

        @Test
        @DisplayName("a field that fails its own constraint -> 200, not valid, and the registry's rules are not run")
        void failsBeanValidation() throws Exception {
            mockMvc.perform(post(BASE + "/$validate").contentType(MediaType.APPLICATION_JSON)
                            .content(validJson("Not A Valid Id", "1.0.0")))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.valid").value(false))
                    .andExpect(jsonPath("$.data.errors[0]").value(startsWith("id: ")))
                    .andExpect(jsonPath("$.data.definition.id").value("Not A Valid Id"));

            verify(registry, never()).preview(any());
        }

        @Test
        @DisplayName("a key that is not part of the contract -> 200, not valid, naming the key")
        void unknownKey() throws Exception {
            mockMvc.perform(post(BASE + "/$validate").contentType(YAML).content(VALID_YAML + "colour: blue\n"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.valid").value(false))
                    .andExpect(jsonPath("$.data.errors[0]").value(containsString("unknown property 'colour'")))
                    .andExpect(jsonPath("$.data.definition").doesNotExist());
        }

        @Test
        @DisplayName("a body that is not YAML or JSON -> 200, not valid, saying it could not be read")
        void unreadable() throws Exception {
            mockMvc.perform(post(BASE + "/$validate").contentType(MediaType.APPLICATION_JSON).content("{ not json"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.valid").value(false))
                    .andExpect(jsonPath("$.data.errors[0]").value(startsWith("Malformed JSON rubric payload")))
                    .andExpect(jsonPath("$.data.definition").doesNotExist());
        }

        @Test
        @DisplayName("no body -> 400")
        void noBody() throws Exception {
            mockMvc.perform(post(BASE + "/$validate").contentType(MediaType.APPLICATION_JSON))
                    .andExpect(status().isBadRequest());
        }

        @Test
        @DisplayName("a content type that is neither JSON nor YAML -> 415")
        void wrongContentType() throws Exception {
            mockMvc.perform(post(BASE + "/$validate").contentType(MediaType.TEXT_PLAIN).content("id: x"))
                    .andExpect(status().isUnsupportedMediaType());
        }
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("GET /{rubricId}/versions/{semver}/definition  (export)")
    class Export {

        private RubricVersion stored() throws Exception {
            RubricVersion v = version(1L, "piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);
            // stored compact, as registering stores it
            v.setDefinitionJson(new ObjectMapper().readTree(validJson("piqi.core", "1.0.0")).toString());
            return v;
        }

        @Test
        @DisplayName("JSON by default, as a file named after the rubric and version")
        void json() throws Exception {
            when(registry.getVersion("piqi.core", "1.0.0")).thenReturn(stored());

            mockMvc.perform(get(BASE + "/piqi.core/versions/1.0.0/definition"))
                    .andExpect(status().isOk())
                    .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON))
                    .andExpect(header().string(HttpHeaders.CONTENT_DISPOSITION,
                            containsString("filename=\"piqi.core-1.0.0.json\"")))
                    // the definition alone, not wrapped in the envelope
                    .andExpect(jsonPath("$.id").value("piqi.core"))
                    .andExpect(jsonPath("$.status").doesNotExist())
                    .andExpect(jsonPath("$.data").doesNotExist());
        }

        @Test
        @DisplayName("YAML when asked for, readable back by the strict reader as the same definition")
        void yamlRoundTrips() throws Exception {
            RubricVersion stored = stored();
            when(registry.getVersion("piqi.core", "1.0.0")).thenReturn(stored);

            MvcResult result = mockMvc.perform(get(BASE + "/piqi.core/versions/1.0.0/definition").accept(YAML))
                    .andExpect(status().isOk())
                    .andExpect(content().contentTypeCompatibleWith(YAML))
                    .andExpect(header().string(HttpHeaders.CONTENT_DISPOSITION,
                            containsString("filename=\"piqi.core-1.0.0.yaml\"")))
                    .andReturn();
            String yaml = result.getResponse().getContentAsString();

            assertFalse(yaml.startsWith("---"), "no document start marker");
            assertTrue(yaml.contains("id: piqi.core"), "plain strings are not quoted");
            // "001" must stay a string: unquoted it would read back as the number 1
            assertTrue(yaml.contains("code: \"001\""), "a string that looks like a number stays quoted: " + yaml);

            YAMLMapper strict = new YAMLMapper();
            strict.enable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);
            ObjectMapper json = new ObjectMapper();
            RubricVersionPayloadDto readBack = strict.readValue(yaml, RubricVersionPayloadDto.class);
            assertEquals(json.readTree(stored.getDefinitionJson()), json.valueToTree(readBack));
        }

        @Test
        @DisplayName("the exported YAML is accepted by $validate as it is")
        void exportedYamlValidates() throws Exception {
            when(registry.getVersion("piqi.core", "1.0.0")).thenReturn(stored());
            when(registry.preview(any())).thenReturn(new DefinitionPreview("sum-1", null, true));
            String yaml = mockMvc.perform(get(BASE + "/piqi.core/versions/1.0.0/definition").accept(YAML))
                    .andReturn().getResponse().getContentAsString();

            mockMvc.perform(post(BASE + "/$validate").contentType(YAML).content(yaml))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.data.valid").value(true))
                    .andExpect(jsonPath("$.data.definition.checks[0].parameters.code").value("001"));
        }

        @Test
        @DisplayName("unknown version -> 404 in the envelope")
        void notFound() throws Exception {
            when(registry.getVersion("piqi.core", "9.9.9"))
                    .thenThrow(new RubricVersionNotFoundException("piqi.core", "9.9.9"));

            mockMvc.perform(get(BASE + "/piqi.core/versions/9.9.9/definition"))
                    .andExpect(status().isNotFound())
                    .andExpect(jsonPath("$.status").value(404));
        }

        @Test
        @DisplayName("a version stored without a definition -> 404")
        void noStoredDefinition() throws Exception {
            when(registry.getVersion("piqi.core", "1.0.0"))
                    .thenReturn(version(1L, "piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED));

            mockMvc.perform(get(BASE + "/piqi.core/versions/1.0.0/definition")).andExpect(status().isNotFound());
        }
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("POST /{rubricId}/versions  (save preconditions)")
    class SavePreconditions {

        private RubricSavePrecondition savedWith(String ifMatch, String ifNoneMatch) throws Exception {
            when(registry.registerVersion(any(), any(), any()))
                    .thenReturn(version(1L, "piqi.core", "1.0.0", RubricVersionStatus.DRAFT));
            var request = post(BASE + "/piqi.core/versions").contentType(MediaType.APPLICATION_JSON)
                    .content(validJson("piqi.core", "1.0.0"));
            if (ifMatch != null) request.header(HttpHeaders.IF_MATCH, ifMatch);
            if (ifNoneMatch != null) request.header(HttpHeaders.IF_NONE_MATCH, ifNoneMatch);
            mockMvc.perform(request).andExpect(status().isCreated());

            ArgumentCaptor<RubricSavePrecondition> precondition = ArgumentCaptor.forClass(RubricSavePrecondition.class);
            verify(registry).registerVersion(any(), any(), precondition.capture());
            return precondition.getValue();
        }

        @Test
        @DisplayName("no headers -> no precondition, as before")
        void none() throws Exception {
            assertTrue(savedWith(null, null).isNone());
        }

        @Test
        @DisplayName("If-Match carries the checksum, as an entity tag")
        void ifMatchQuoted() throws Exception {
            RubricSavePrecondition precondition = savedWith("\"abc123\"", null);
            assertEquals("abc123", precondition.expectedChecksum());
            assertFalse(precondition.mustNotExist());
        }

        @Test
        @DisplayName("If-Match is also read bare or as a weak entity tag")
        void ifMatchOtherForms() {
            assertEquals("abc123", RubricController.precondition("abc123", null).expectedChecksum());
            assertEquals("abc123", RubricController.precondition("W/\"abc123\"", null).expectedChecksum());
            assertEquals("abc123", RubricController.precondition("  \"abc123\" ", null).expectedChecksum());
        }

        @Test
        @DisplayName("If-Match: * and a blank If-Match are no precondition")
        void ifMatchAny() {
            assertTrue(RubricController.precondition("*", null).isNone());
            assertTrue(RubricController.precondition(" ", null).isNone());
        }

        @Test
        @DisplayName("If-None-Match: * says the version must not exist yet")
        void ifNoneMatch() throws Exception {
            RubricSavePrecondition precondition = savedWith(null, "*");
            assertTrue(precondition.mustNotExist());
            assertNull(precondition.expectedChecksum());
        }

        @Test
        @DisplayName("an If-None-Match that is not * is no precondition")
        void ifNoneMatchOther() {
            assertTrue(RubricController.precondition(null, "\"abc123\"").isNone());
        }

        @Test
        @DisplayName("a precondition that does not hold -> 412 in the envelope")
        void preconditionFailed() throws Exception {
            when(registry.registerVersion(any(), any(), any()))
                    .thenThrow(RubricPreconditionFailedException.changed("piqi.core", "1.0.0"));

            mockMvc.perform(post(BASE + "/piqi.core/versions").contentType(MediaType.APPLICATION_JSON)
                            .header(HttpHeaders.IF_MATCH, "\"stale\"")
                            .content(validJson("piqi.core", "1.0.0")))
                    .andExpect(status().isPreconditionFailed())
                    .andExpect(jsonPath("$.status").value(412))
                    .andExpect(jsonPath("$.message").value(containsString("has been changed since it was read")));
        }
    }

    // ---------------------------------------------------------------------------------------------
    @Nested
    @DisplayName("DELETE /{rubricId}/versions/{semver}")
    class DeleteDraft {

        @Test
        @DisplayName("a draft -> 204 with no body")
        void deleted() throws Exception {
            mockMvc.perform(delete(BASE + "/piqi.core/versions/1.0.0"))
                    .andExpect(status().isNoContent())
                    .andExpect(content().string(""));

            verify(registry).deleteDraft("piqi.core", "1.0.0", "anonymous");
        }

        @Test
        @DisplayName("a published or retired version -> 409")
        void notADraft() throws Exception {
            doThrow(new RubricLifecycleException("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED, "delete"))
                    .when(registry).deleteDraft(any(), any(), any());

            mockMvc.perform(delete(BASE + "/piqi.core/versions/1.0.0"))
                    .andExpect(status().isConflict())
                    .andExpect(jsonPath("$.message").value("Cannot delete piqi.core v1.0.0: status is PUBLISHED"));
        }

        @Test
        @DisplayName("unknown version -> 404")
        void notFound() throws Exception {
            doThrow(new RubricVersionNotFoundException("piqi.core", "9.9.9"))
                    .when(registry).deleteDraft(any(), any(), any());

            mockMvc.perform(delete(BASE + "/piqi.core/versions/9.9.9")).andExpect(status().isNotFound());
        }
    }
}
