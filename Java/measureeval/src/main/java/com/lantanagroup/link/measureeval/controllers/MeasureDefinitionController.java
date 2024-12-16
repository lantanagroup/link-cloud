package com.lantanagroup.link.measureeval.controllers;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.serdes.Views;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionBundleValidator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import com.lantanagroup.link.shared.auth.PrincipalUser;
import io.opentelemetry.api.trace.Span;
import io.swagger.v3.oas.annotations.Operation;
import org.apache.commons.text.StringEscapeUtils;
import org.hl7.fhir.r4.model.*;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.WebDataBinder;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;
import java.util.Optional;

@RestController
@RequestMapping("/api/measure-definition")
@PreAuthorize("hasRole('LinkUser')")
public class MeasureDefinitionController {

    private final Logger _logger = LoggerFactory.getLogger(MeasureDefinitionController.class);
    private final MeasureDefinitionRepository repository;
    private final MeasureDefinitionBundleValidator bundleValidator;
    private final MeasureEvaluatorCache evaluatorCache;

    final String[] DISALLOWED_FIELDS = new String[]{};
    @InitBinder
    public void initBinder(WebDataBinder binder) {
        binder.setDisallowedFields(DISALLOWED_FIELDS);
    }

    public MeasureDefinitionController(
            MeasureDefinitionRepository repository,
            MeasureDefinitionBundleValidator bundleValidator,
            MeasureEvaluatorCache evaluatorCache){
        this.repository = repository;
        this.bundleValidator = bundleValidator;
        this.evaluatorCache = evaluatorCache;
    }

    @GetMapping
    @JsonView(Views.Summary.class)
    @Operation(summary = "Get all measure definitions", tags = {"Measure Definitions"})
    public List<MeasureDefinition> getAll(@AuthenticationPrincipal PrincipalUser user) {
        _logger.info("Get all measure definitions");

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }
        return repository.findAll();

    }

    @GetMapping("/{id}")
    @Operation(summary = "Get a measure definition", tags = {"Measure Definitions"})
    public MeasureDefinition getOne(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id) {

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }

        return repository.findById(id).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
    }

    @PutMapping("/{id}")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Put (create or update) a measure definition", tags = {"Measure Definitions"})
    public MeasureDefinition put(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id, @RequestBody Bundle bundle) {
        _logger.info("Put measure definition {}", StringEscapeUtils.escapeJava(id));

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }
        bundleValidator.validate(bundle);
        MeasureDefinition entity = repository.findById(id).orElseGet(() -> {
            MeasureDefinition _entity = new MeasureDefinition();
            _entity.setId(id);
            return _entity;
        });
        entity.setBundle(bundle);
        repository.save(entity);
        evaluatorCache.remove(id);
        return entity;
    }

    private static StringBuilder getRangeCql(String range, String cql) {
        String[] rangeParts = range.split(":|-");
        int startLine = Integer.parseInt(rangeParts[0]);
        int startColumn = Integer.parseInt(rangeParts[1]);
        int endLine = Integer.parseInt(rangeParts[2]);
        int endColumn = Integer.parseInt(rangeParts[3]);

        // Get the lines from the CQL
        String[] lines = cql.split("\n");

        // Get the lines in the range
        StringBuilder rangeCql = new StringBuilder();
        for (int i = startLine - 1; i < endLine; i++) {

            if (i == startLine - 1) {
                rangeCql.append(lines[i].substring(startColumn - 1));
            } else if (i == endLine - 1) {
                rangeCql.append(lines[i].substring(0, endColumn));
            } else {
                rangeCql.append(lines[i]);
            }
            if (i != endLine - 1) {
                rangeCql.append("\n");
            }
        }
        return rangeCql;
    }

    @GetMapping("/{id}/{library-id}/$cql")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Get the CQL for a measure definition's library", tags = {"Measure Definitions"})
    public String getMeasureLibraryCQL(
            @PathVariable("id") String measureId,
            @PathVariable("library-id") String libraryId,
            @RequestParam(value = "range", required = false) String range) {

        // Test that the range format is correct (i.e. "37:1-38:22")
        if (range != null && !range.matches("\\d+:\\d+-\\d+:\\d+")) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Invalid range format");
        }

        // Get the measure definition from the repo by ID
        MeasureDefinition measureDefinition = repository.findById(measureId).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));

        // Get library from the measure definition bundle based on the libraryId
        Optional<Library> library = measureDefinition.getBundle().getEntry().stream()
                .filter(entry -> {
                    if (!entry.hasResource() || entry.getResource().getResourceType() != ResourceType.Library) {
                        return false;
                    }

                    Library l = (Library) entry.getResource();

                    if (l.getUrl() == null) {
                        return false;
                    }

                    return l.getUrl().endsWith("/" + libraryId);
                })
                .findFirst()
                .map(entry -> (Library) entry.getResource());

        if (library.isEmpty()) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "Library not found in measure definition bundle");
        }

        // Get CQL from library's "content" and base64 decode it
        String cql = library.get().getContent().stream()
                .filter(content -> content.hasContentType() && content.getContentType().equals("text/cql"))
                .findFirst()
                .map(content -> new String(content.getData()))
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "CQL content not found in library"));

        // Find range in CQL
        if (range != null) {
            // Split range into start and end line/column
            StringBuilder rangeCql = getRangeCql(range, cql);

            return rangeCql.toString();
        }

        return cql;
    }

    @PostMapping("/{id}/$evaluate")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Evaluate a measure against data in request body", tags = {"Measure Definitions"})
    public MeasureReport evaluate(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id, @RequestBody Parameters parameters, @RequestParam(required = false, defaultValue = "false") boolean debug) {

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }

        try {
            // Compile the measure every time because the debug flag may have changed from what's in the cache
            MeasureDefinition measureDefinition = repository.findById(id).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
            return MeasureEvaluator.compileAndEvaluate(FhirContext.forR4(), measureDefinition.getBundle(), parameters, debug);
        } catch (Exception e) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, e.getMessage(), e);
        }
    }
}
