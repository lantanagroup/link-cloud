package com.lantanagroup.link.validation.controllers;

import com.fasterxml.jackson.core.JacksonException;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.exc.UnrecognizedPropertyException;
import com.fasterxml.jackson.dataformat.yaml.YAMLMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import com.lantanagroup.link.validation.models.RubricDetailDto;
import com.lantanagroup.link.validation.models.RubricLifecycleEventDto;
import com.lantanagroup.link.validation.models.RubricSummaryDto;
import com.lantanagroup.link.validation.models.RubricVersionDetailDto;
import com.lantanagroup.link.validation.models.RubricVersionPayloadDto;
import com.lantanagroup.link.validation.models.RubricVersionSummaryDto;
import com.lantanagroup.link.validation.records.ApiResponse;
import com.lantanagroup.link.validation.records.PagedData;
import com.lantanagroup.link.validation.services.RubricRegistryService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Validator;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.data.domain.Pageable;
import org.springframework.data.web.PageableDefault;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
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

@RestController
@RequestMapping("/api/validation/rubrics")
@SecurityRequirement(name = "bearer-key")
@Tag(name = "Rubric Registry", description = "Authoring and lifecycle: register, publish, retire")
public class RubricController {

    private static final String APPLICATION_YAML_VALUE = "application/yaml";
    private static final String APPLICATION_X_YAML_VALUE = "application/x-yaml";
    private static final String TEXT_YAML_VALUE = "text/yaml";

    private final RubricRegistryService registry;
    private final ObjectMapper objectMapper;
    private final Validator validator;
    // Strict copies used only for request parsing: unknown payload keys are typos, not extensions.
    // The shared Spring ObjectMapper stays lenient for response serialization.
    private final ObjectMapper strictJsonMapper;
    private final YAMLMapper strictYamlMapper;
    private final int maxPayloadBytes;

    public RubricController(RubricRegistryService registry,
                            ObjectMapper objectMapper,
                            Validator validator,
                            @Value("${link.rubric.max-payload-bytes:262144}") int maxPayloadBytes) {
        this.registry = registry;
        this.objectMapper = objectMapper;
        this.validator = validator;
        this.maxPayloadBytes = maxPayloadBytes;
        this.strictJsonMapper = objectMapper.copy()
                .enable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);
        this.strictYamlMapper = new YAMLMapper();
        this.strictYamlMapper.enable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);
    }

    @Operation(summary = "List rubrics (paged), each with all its versions and their status")
    @GetMapping
    public ResponseEntity<ApiResponse<PagedData<RubricSummaryDto>>> listRubrics(
            @PageableDefault(size = 20, sort = "rubricId") Pageable pageable) {
        var rubricPage = registry.listRubrics(pageable);
        List<String> rubricIds = rubricPage.getContent().stream().map(Rubric::getRubricId).toList();
        Map<String, List<RubricVersion>> versionsByRubric = registry.versionsByRubricId(rubricIds);
        PagedData<RubricSummaryDto> page = PagedData.from(rubricPage, r ->
                RubricSummaryDto.from(r, versionSummaries(versionsByRubric.getOrDefault(r.getRubricId(), List.of()))));
        return ResponseEntity.ok(ApiResponse.ok("Rubrics fetched successfully", page));
    }

    @Operation(summary = "Get a rubric with all its versions (each with status and per-version metadata)")
    @GetMapping("/{rubricId}")
    public ResponseEntity<ApiResponse<RubricDetailDto>> getRubric(@PathVariable String rubricId) {
        Rubric rubric = registry.getRubric(rubricId);
        String latest = registry.latestPublishedSemver(rubricId).orElse(null);
        List<RubricVersionSummaryDto> versions = versionSummaries(registry.listVersions(rubricId, null));
        return ResponseEntity.ok(ApiResponse.ok("Rubric fetched successfully",
                RubricDetailDto.from(rubric, latest, versions)));
    }

    @Operation(summary = "List versions for a rubric, optionally filtered by status")
    @GetMapping("/{rubricId}/versions")
    public ResponseEntity<ApiResponse<List<RubricVersionSummaryDto>>> listVersions(
            @PathVariable String rubricId,
            @RequestParam(required = false) RubricVersionStatus status) {
        List<RubricVersionSummaryDto> versions = versionSummaries(registry.listVersions(rubricId, status));
        return ResponseEntity.ok(ApiResponse.ok("Rubric versions fetched successfully", versions));
    }

    private List<RubricVersionSummaryDto> versionSummaries(List<RubricVersion> versions) {
        return versions.stream().map(v -> RubricVersionSummaryDto.from(v, objectMapper)).toList();
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

    @Operation(summary = "Register a new rubric version (status = DRAFT); a semver can be registered exactly once. "
            + "The payload may be submitted as JSON or YAML (Content-Type: application/yaml).")
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "201"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "400"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "409", description = "This semver is already registered (even with identical content)")
    })
    @PostMapping(value = "/{rubricId}/versions",
            consumes = {MediaType.APPLICATION_JSON_VALUE, APPLICATION_YAML_VALUE, APPLICATION_X_YAML_VALUE, TEXT_YAML_VALUE})
    public ResponseEntity<ApiResponse<RubricVersionSummaryDto>> registerVersion(
            @PathVariable String rubricId,
            @RequestBody String rawBody,
            @RequestHeader(value = HttpHeaders.CONTENT_TYPE, required = false) String contentType,
            Principal principal) {
        if (rawBody.getBytes(StandardCharsets.UTF_8).length > maxPayloadBytes) {
            throw new PayloadParseException(
                    "Rubric payload exceeds maximum size of " + maxPayloadBytes + " bytes", null);
        }
        RubricVersionPayloadDto payload = parsePayload(rawBody, isYaml(contentType));
        validatePayload(payload);
        if (!rubricId.equals(payload.getId())) {
            throw new InvalidRubricDefinitionException("Rubric id mismatch",
                    List.of("id: body id '" + payload.getId() + "' does not match path id '" + rubricId + "'"));
        }
        RubricVersion version = registry.registerVersion(payload, actor(principal));
        RubricVersionSummaryDto dto = RubricVersionSummaryDto.from(version, objectMapper);
        URI location = ServletUriComponentsBuilder.fromCurrentRequest()
                .path("/{semver}")
                .buildAndExpand(payload.getSemver())
                .toUri();
        return ResponseEntity.created(location)
                .body(ApiResponse.created("Rubric version registered", dto));
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
            // the document is well-formed; the key is just not part of the contract -> 400 with errors list
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
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "409", description = "Version is already published, or retired")
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

    @Operation(summary = "Mark a PUBLISHED version RETIRED (the only legal transition into RETIRED)")
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "200"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "404"),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(responseCode = "409", description = "Version is already retired, or still a draft")
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
