package com.lantanagroup.link.validation.controllers;

import com.fasterxml.jackson.core.JacksonException;
import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.exc.UnrecognizedPropertyException;
import com.fasterxml.jackson.dataformat.yaml.YAMLGenerator;
import com.fasterxml.jackson.dataformat.yaml.YAMLMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.InvalidRubricRequestException;
import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.RubricDetailDto;
import com.lantanagroup.link.validation.models.RubricLifecycleEventDto;
import com.lantanagroup.link.validation.models.RubricSettingsDto;
import com.lantanagroup.link.validation.models.RubricSummaryDto;
import com.lantanagroup.link.validation.models.RubricValidationResultDto;
import com.lantanagroup.link.validation.models.RubricVersionDetailDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.models.RubricVersionSummaryDto;
import com.lantanagroup.link.validation.records.ApiResponse;
import com.lantanagroup.link.validation.records.PagedData;
import com.lantanagroup.link.validation.records.RubricSavePrecondition;
import com.lantanagroup.link.validation.records.RubricSearch;
import com.lantanagroup.link.validation.services.RubricRegistryService;
import com.lantanagroup.link.validation.services.RubricSettingsService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Validator;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Pageable;
import org.springframework.data.domain.Sort;
import org.springframework.data.web.PageableDefault;
import org.springframework.http.ContentDisposition;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.support.ServletUriComponentsBuilder;

import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.security.Principal;
import java.util.List;
import java.util.Map;
import java.util.Set;

@RestController
@RequestMapping("/api/validation/rubrics")
@SecurityRequirement(name = "bearer-key")
@Tag(name = "Rubric Registry", description = "Authoring and lifecycle: register, publish, retire")
public class RubricController {

    private static final String APPLICATION_YAML_VALUE = "application/yaml";
    private static final String APPLICATION_X_YAML_VALUE = "application/x-yaml";
    private static final String TEXT_YAML_VALUE = "text/yaml";

    // What the list can be sorted by. A sort on anything else is refused rather than passed to the query.
    private static final Set<String> SORTABLE = Set.of("rubricId", "title", "owner", "createdAt", "updatedAt");

    private final RubricRegistryService registry;
    private final RubricSettingsService settings;
    private final ObjectMapper objectMapper;
    private final Validator validator;
    // Strict copies used only for request parsing: unknown payload keys are treated as typos,
// not extensions. The shared Spring ObjectMapper remains lenient for response serialization.
    private final ObjectMapper strictJsonMapper;
    private final YAMLMapper strictYamlMapper;
    // Writes a definition as YAML a person can read and this controller's strict reader takes back unchanged.
    private final YAMLMapper yamlWriter;
    private final int maxPayloadBytes;

    public RubricController(RubricRegistryService registry,
                            RubricSettingsService settings,
                            ObjectMapper objectMapper,
                            Validator validator,
                            @Value("${link.rubric.max-payload-bytes:262144}") int maxPayloadBytes) {
        this.registry = registry;
        this.settings = settings;
        this.objectMapper = objectMapper;
        this.validator = validator;
        this.maxPayloadBytes = maxPayloadBytes;
        this.strictJsonMapper = objectMapper.copy()
                .enable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);
        this.strictJsonMapper.getFactory().enable(JsonParser.Feature.STRICT_DUPLICATE_DETECTION);
        this.strictYamlMapper = new YAMLMapper();
        this.strictYamlMapper.enable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);
        this.strictYamlMapper.getFactory().enable(JsonParser.Feature.STRICT_DUPLICATE_DETECTION);
        // No "---" line, and no quotes except where a string would otherwise read back as something else:
        // text that looks like a number ("001") or a keyword ("true") stays quoted, so it stays a string.
        this.yamlWriter = YAMLMapper.builder()
                .disable(YAMLGenerator.Feature.WRITE_DOC_START_MARKER)
                .enable(YAMLGenerator.Feature.MINIMIZE_QUOTES)
                .enable(YAMLGenerator.Feature.ALWAYS_QUOTE_NUMBERS_AS_STRINGS)
                .build();
    }

    @Operation(summary = "List rubrics (paged), each with its versions. Optional filters: status (only rubrics "
            + "having a version in that status), search (text found in the id, title or owner) and owner (exact). "
            + "With a status the versions are trimmed to it, unless allVersions=true. "
            + "Sortable by rubricId, title, owner, createdAt and updatedAt")
    @GetMapping
    public ResponseEntity<ApiResponse<PagedData<RubricSummaryDto>>> listRubrics(
            @RequestParam(required = false) RubricVersionStatus status,
            @RequestParam(required = false) String search,
            @RequestParam(required = false) String owner,
            @RequestParam(required = false, defaultValue = "false") boolean allVersions,
            @PageableDefault(size = 20, sort = "rubricId") Pageable pageable) {
        var rubricPage = registry.searchRubrics(new RubricSearch(status, search, owner), sortable(pageable));
        List<String> rubricIds = rubricPage.getContent().stream().map(Rubric::getRubricId).toList();
        Map<String, List<RubricVersion>> versionsByRubric =
                registry.versionsByRubricId(rubricIds, allVersions ? null : status);
        Map<Long, Integer> checkCounts = checkCounts(versionsByRubric.values().stream().flatMap(List::stream).toList());
        PagedData<RubricSummaryDto> page = PagedData.from(rubricPage, r -> RubricSummaryDto.from(r,
                versionSummaries(versionsByRubric.getOrDefault(r.getRubricId(), List.of()), checkCounts)));
        return ResponseEntity.ok(ApiResponse.ok("Rubrics fetched successfully", page));
    }

    // A sort on a property the list does not offer is a 400, not a query on whatever was named. Sorting
    // always ends on the id, so rubrics that tie on the chosen property keep one order from page to page.
    private static Pageable sortable(Pageable pageable) {
        for (Sort.Order order : pageable.getSort()) {
            if (!SORTABLE.contains(order.getProperty())) {
                throw new InvalidRubricRequestException("Cannot sort by '" + order.getProperty()
                        + "'; use one of " + SORTABLE.stream().sorted().toList());
            }
        }
        // Only when the sort does not name the id already: SQL Server refuses a column given twice in ORDER BY.
        Sort sort = pageable.getSort().getOrderFor("rubricId") != null
                ? pageable.getSort()
                : pageable.getSort().and(Sort.by("rubricId"));
        return PageRequest.of(pageable.getPageNumber(), pageable.getPageSize(), sort);
    }

    @Operation(summary = "The owners rubrics have, in order, for a filter to offer")
    @GetMapping("/$owners")
    public ResponseEntity<ApiResponse<List<String>>> listOwners() {
        return ResponseEntity.ok(ApiResponse.ok("Rubric owners fetched successfully", registry.listOwners()));
    }

    @Operation(summary = "How this service treats rubrics: the limits and values a definition must keep to, whether "
            + "publishing requires a dry run, and which rubric the reporting pipeline uses")
    @GetMapping("/$settings")
    public ResponseEntity<ApiResponse<RubricSettingsDto>> getSettings() {
        return ResponseEntity.ok(ApiResponse.ok("Rubric settings fetched successfully", settings.getSettings()));
    }

    @Operation(summary = "Get a rubric with all its versions (each with status and per-version metadata)")
    @GetMapping("/{rubricId}")
    public ResponseEntity<ApiResponse<RubricDetailDto>> getRubric(@PathVariable String rubricId) {
        Rubric rubric = registry.getRubric(rubricId);
        String latest = registry.latestPublishedSemver(rubricId).orElse(null);
        List<RubricVersion> all = registry.listVersions(rubricId, null);
        List<RubricVersionSummaryDto> versions = versionSummaries(all, checkCounts(all));
        return ResponseEntity.ok(ApiResponse.ok("Rubric fetched successfully",
                RubricDetailDto.from(rubric, latest, versions)));
    }

    @Operation(summary = "List versions for a rubric, optionally filtered by status")
    @GetMapping("/{rubricId}/versions")
    public ResponseEntity<ApiResponse<List<RubricVersionSummaryDto>>> listVersions(
            @PathVariable String rubricId,
            @RequestParam(required = false) RubricVersionStatus status) {
        List<RubricVersion> matching = registry.listVersions(rubricId, status);
        List<RubricVersionSummaryDto> versions = versionSummaries(matching, checkCounts(matching));
        return ResponseEntity.ok(ApiResponse.ok("Rubric versions fetched successfully", versions));
    }

    private Map<Long, Integer> checkCounts(List<RubricVersion> versions) {
        return registry.checkCounts(versions.stream().map(RubricVersion::getRubricVersionId).toList());
    }

    // a version absent from the counts has no checks
    private List<RubricVersionSummaryDto> versionSummaries(List<RubricVersion> versions, Map<Long, Integer> checkCounts) {
        return versions.stream()
                .map(v -> RubricVersionSummaryDto.from(v, objectMapper, checkCounts.getOrDefault(v.getRubricVersionId(), 0)))
                .toList();
    }

    @Operation(summary = "Get one version including its full definition and checks")
    @GetMapping("/{rubricId}/versions/{semver}")
    public ResponseEntity<ApiResponse<RubricVersionDetailDto>> getVersion(
            @PathVariable String rubricId,
            @PathVariable String semver) {
        RubricVersion version = registry.getVersion(rubricId, semver);
        RubricVersionDetailDto dto = RubricVersionDetailDto.from(
                version, registry.getChecks(version.getRubricVersionId()), objectMapper);
        return ResponseEntity.ok(ApiResponse.ok("Rubric version fetched successfully", dto));
    }

    @Operation(summary = "A version's definition alone, as a file: YAML when the Accept header asks for it "
            + "(application/yaml), JSON otherwise. Either can be sent back to register or $validate as it is")
    @GetMapping("/{rubricId}/versions/{semver}/definition")
    public ResponseEntity<String> exportDefinition(
            @PathVariable String rubricId,
            @PathVariable String semver,
            @RequestHeader(value = HttpHeaders.ACCEPT, required = false) String accept) {
        RubricVersion version = registry.getVersion(rubricId, semver);
        if (version.getDefinitionJson() == null || version.getDefinitionJson().isBlank()) {
            // a row written before definitions were stored; there is nothing to export
            throw new RubricVersionNotFoundException(rubricId, version.getSemver());
        }
        boolean yaml = isYaml(accept);
        String body;
        try {
            JsonNode definition = objectMapper.readTree(version.getDefinitionJson());
            body = yaml
                    ? yamlWriter.writeValueAsString(definition)
                    : objectMapper.writerWithDefaultPrettyPrinter().writeValueAsString(definition);
        } catch (JacksonException e) {
            throw new IllegalStateException("Stored definition of " + rubricId + " v" + version.getSemver()
                    + " could not be read", e);
        }
        String fileName = version.getRubricId() + "-" + version.getSemver() + (yaml ? ".yaml" : ".json");
        return ResponseEntity.ok()
                .contentType(yaml
                        ? new MediaType("application", "yaml", StandardCharsets.UTF_8)
                        : MediaType.APPLICATION_JSON)
                .header(HttpHeaders.CONTENT_DISPOSITION,
                        ContentDisposition.attachment().filename(fileName).build().toString())
                .body(body);
    }

    @Operation(summary = "Check a rubric definition without saving it. Runs the same rules as registering a version "
            + "and answers 200 with what it found: valid or not, every problem, the definition as JSON, and the "
            + "version already registered under its id and semver, if any. The payload may be JSON or YAML")
    @PostMapping(value = "/$validate",
            consumes = {MediaType.APPLICATION_JSON_VALUE, APPLICATION_YAML_VALUE, APPLICATION_X_YAML_VALUE, TEXT_YAML_VALUE})
    public ResponseEntity<ApiResponse<RubricValidationResultDto>> validateDefinition(
            @RequestBody String rawBody,
            @RequestHeader(value = HttpHeaders.CONTENT_TYPE, required = false) String contentType) {
        if (utf8ByteLength(rawBody) > maxPayloadBytes) {
            throw new PayloadParseException(
                    "Rubric payload exceeds maximum size of " + maxPayloadBytes + " bytes", null);
        }

        RubricVersionPayloadDto payload;
        try {
            payload = parsePayload(rawBody, isYaml(contentType));
        } catch (InvalidRubricDefinitionException e) {
            // well-formed, but with a key that is not part of the contract: nothing typed to show
            return validationResult(RubricValidationResultDto.builder().valid(false).errors(e.getErrors()).build());
        } catch (PayloadParseException e) {
            return validationResult(RubricValidationResultDto.builder().valid(false).errors(List.of(e.getMessage())).build());
        }

        RubricValidationResultDto.RubricValidationResultDtoBuilder result = RubricValidationResultDto.builder();
        try {
            validatePayload(payload);
            RubricRegistryService.DefinitionPreview preview = registry.preview(payload);
            result.valid(true).errors(List.of()).checksum(preview.checksum()).rubricExists(preview.rubricExists());
            describeExisting(result, preview.existing());
        } catch (InvalidRubricDefinitionException e) {
            result.valid(false).errors(e.getErrors()).rubricExists(registry.rubricExists(payload.getId()));
            describeExisting(result, registry.existingVersion(payload).orElse(null));
        }
        // the definition is returned whether it is valid or not: a caller that sent YAML needs it as JSON
        // to show what is wrong where
        return validationResult(result.definition(objectMapper.valueToTree(payload)).build());
    }

    private static void describeExisting(RubricValidationResultDto.RubricValidationResultDtoBuilder result,
                                         RubricVersion existing) {
        if (existing != null) {
            result.existingStatus(existing.getStatus()).existingChecksum(existing.getChecksum());
        }
    }

    private static ResponseEntity<ApiResponse<RubricValidationResultDto>> validationResult(RubricValidationResultDto result) {
        return ResponseEntity.ok(ApiResponse.ok(
                result.isValid() ? "Rubric definition is valid" : "Rubric definition is not valid", result));
    }

    @Operation(summary = "Register a rubric version (status = DRAFT). Re-registering a semver that is still a "
            + "DRAFT replaces its definition and checks in place; a PUBLISHED or RETIRED semver is immutable. "
            + "The payload may be submitted as JSON or YAML (Content-Type: application/yaml). "
            + "Optional preconditions: If-Match with the checksum of the draft last read refuses the save (412) "
            + "when the draft has since changed or been deleted; If-None-Match: * refuses it (412) when the "
            + "version already exists.")
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "201"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "400"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "409", description = "This semver is already PUBLISHED or RETIRED and cannot be re-registered"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "412", description = "An If-Match or If-None-Match precondition did not hold")
    })
    @PostMapping(value = "/{rubricId}/versions",
            consumes = {MediaType.APPLICATION_JSON_VALUE, APPLICATION_YAML_VALUE, APPLICATION_X_YAML_VALUE, TEXT_YAML_VALUE})
    public ResponseEntity<ApiResponse<RubricVersionSummaryDto>> registerVersion(
            @PathVariable String rubricId,
            @RequestBody String rawBody,
            @RequestHeader(value = HttpHeaders.CONTENT_TYPE, required = false) String contentType,
            @RequestHeader(value = HttpHeaders.IF_MATCH, required = false) String ifMatch,
            @RequestHeader(value = HttpHeaders.IF_NONE_MATCH, required = false) String ifNoneMatch,
            Principal principal) {
        // Secondary safeguard behind RubricPayloadLimitConfig for chunked requests
        // without a Content-Length header.
        if (utf8ByteLength(rawBody) > maxPayloadBytes) {
            throw new PayloadParseException(
                    "Rubric payload exceeds maximum size of " + maxPayloadBytes + " bytes", null);
        }
        RubricVersionPayloadDto payload = parsePayload(rawBody, isYaml(contentType));
        validatePayload(payload);
        if (!rubricId.equals(payload.getId())) {
            throw new InvalidRubricDefinitionException("Rubric id mismatch",
                    List.of("id: body id '" + payload.getId() + "' does not match path id '" + rubricId + "'"));
        }
        RubricVersion version = registry.registerVersion(payload, actor(principal), precondition(ifMatch, ifNoneMatch));
        RubricVersionSummaryDto dto = RubricVersionSummaryDto.from(version, objectMapper);
        // Build the Location from the persisted version's canonical semver rather than the raw payload,
        // so the header and body agree even when the input contains leading zeros.
        URI location = ServletUriComponentsBuilder.fromCurrentRequest()
                .path("/{semver}")
                .buildAndExpand(version.getSemver())
                .toUri();
        return ResponseEntity.created(location)
                .body(ApiResponse.created("Rubric version registered", dto));
    }

    // If-Match carries the checksum of the draft the caller read, as an entity tag ("abc", or W/"abc") or bare.
    // If-None-Match is only meaningful as "*": the caller is creating the version. "If-Match: *" asks for
    // nothing this endpoint can check, so it is no precondition.
    static RubricSavePrecondition precondition(String ifMatch, String ifNoneMatch) {
        String expected = null;
        if (ifMatch != null && !ifMatch.isBlank() && !"*".equals(ifMatch.trim())) {
            expected = ifMatch.trim();
            if (expected.startsWith("W/")) {
                expected = expected.substring(2);
            }
            if (expected.length() >= 2 && expected.startsWith("\"") && expected.endsWith("\"")) {
                expected = expected.substring(1, expected.length() - 1);
            }
        }
        boolean mustNotExist = ifNoneMatch != null && "*".equals(ifNoneMatch.trim());
        return new RubricSavePrecondition(expected, mustNotExist);
    }

    // counts UTF-8 bytes without materializing a full byte-array copy of the body
    private static long utf8ByteLength(String s) {
        long bytes = 0;
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c < 0x80) bytes += 1;
            else if (c < 0x800) bytes += 2;
            else if (Character.isHighSurrogate(c) && i + 1 < s.length()) { bytes += 4; i++; }
            else bytes += 3;
        }
        return bytes;
    }

    private static boolean isYaml(String contentType) {
        if (contentType == null) return false;
        String normalized = contentType.toLowerCase();
        return normalized.contains(APPLICATION_YAML_VALUE)
                || normalized.contains(APPLICATION_X_YAML_VALUE)
                || normalized.contains(TEXT_YAML_VALUE);
    }

    private RubricVersionPayloadDto parsePayload(String rawBody, boolean isYaml) {
        try {
            return isYaml
                    ? strictYamlMapper.readValue(rawBody, RubricVersionPayloadDto.class)
                    : strictJsonMapper.readValue(rawBody, RubricVersionPayloadDto.class);
        } catch (UnrecognizedPropertyException e) {
            // The document is well-formed, but the key is not part of the contract, so return a 400 with an errors list.
            throw new InvalidRubricDefinitionException("Invalid rubric definition",
                    List.of(pathOf(e) + ": unknown property '" + e.getPropertyName() + "'"));
        } catch (JacksonException e) {
            throw new PayloadParseException(
                    "Malformed " + (isYaml ? "YAML" : "JSON") + " rubric payload: " + e.getOriginalMessage(), e);
        }
    }

    // Renders the location of an unrecognized key, e.g. "payload" (top level) or "checks[0]".
    private static String pathOf(UnrecognizedPropertyException e) {
        StringBuilder path = new StringBuilder();
        for (var ref : e.getPath()) {
            if (ref.getFieldName() != null && !ref.getFieldName().equals(e.getPropertyName())) {
                if (path.length() > 0) path.append('.');
                path.append(ref.getFieldName());
            } else if (ref.getIndex() >= 0) {
                path.append('[').append(ref.getIndex()).append(']');
            }
        }
        return path.length() > 0 ? path.toString() : "payload";
    }

    // Manual binding (raw String body) bypasses @Valid, so run bean validation explicitly.
    private void validatePayload(RubricVersionPayloadDto payload) {
        var violations = validator.validate(payload);
        if (!violations.isEmpty()) {
            List<String> errors = violations.stream()
                    .map(v -> v.getPropertyPath() + ": " + v.getMessage())
                    .sorted()
                    .toList();
            throw new InvalidRubricDefinitionException("Invalid rubric definition", errors);
        }
    }

    @Operation(summary = "Promote a DRAFT to PUBLISHED (the only legal transition into PUBLISHED)")
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "200"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "404"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "409", description = "Version is already published or retired, or dry-run enforcement is enabled and no acceptable dry run exists for this version")
    })
    @PostMapping("/{rubricId}/versions/{semver}/$publish")
    public ResponseEntity<ApiResponse<RubricVersionSummaryDto>> publish(
            @PathVariable String rubricId,
            @PathVariable String semver,
            Principal principal) {
        RubricVersion version = registry.publish(rubricId, semver, actor(principal));
        return ResponseEntity.ok(ApiResponse.ok("Rubric version published",
                RubricVersionSummaryDto.from(version, objectMapper)));
    }

    @Operation(summary = "Mark a version RETIRED. Both DRAFT (abandon without publishing) and PUBLISHED versions can be retired")
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "200"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "404"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "409", description = "Version is already retired")
    })
    @PostMapping("/{rubricId}/versions/{semver}/$retire")
    public ResponseEntity<ApiResponse<RubricVersionSummaryDto>> retire(
            @PathVariable String rubricId,
            @PathVariable String semver,
            Principal principal) {
        RubricVersion version = registry.retire(rubricId, semver, actor(principal));
        return ResponseEntity.ok(ApiResponse.ok("Rubric version retired",
                RubricVersionSummaryDto.from(version, objectMapper)));
    }

    @Operation(summary = "Delete a version that is still a DRAFT. A PUBLISHED or RETIRED version cannot be deleted. "
            + "Deleting a rubric's only version removes the rubric")
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "204"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "404"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "409", description = "Version is PUBLISHED or RETIRED")
    })
    @DeleteMapping("/{rubricId}/versions/{semver}")
    public ResponseEntity<Void> deleteDraft(
            @PathVariable String rubricId,
            @PathVariable String semver,
            Principal principal) {
        registry.deleteDraft(rubricId, semver, actor(principal));
        return ResponseEntity.noContent().build();
    }

    @Operation(summary = "Lifecycle audit trail for a rubric")
    @GetMapping("/{rubricId}/events")
    public ResponseEntity<ApiResponse<List<RubricLifecycleEventDto>>> listEvents(@PathVariable String rubricId) {
        List<RubricLifecycleEventDto> events = registry.listLifecycleEvents(rubricId).stream()
                .map(RubricLifecycleEventDto::from)
                .toList();
        return ResponseEntity.ok(ApiResponse.ok("Lifecycle events fetched successfully", events));
    }

    private String actor(Principal principal) {
        return principal != null ? principal.getName() : "anonymous";
    }
}
