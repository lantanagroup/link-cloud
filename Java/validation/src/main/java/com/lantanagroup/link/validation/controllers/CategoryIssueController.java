package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.CategoryIssuePage;
import com.lantanagroup.link.validation.services.ResultService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import org.springframework.http.ProblemDetail;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

@RestController
@RequestMapping("/api/validation/facilities")
@SecurityRequirement(name = "bearer-key")
public class CategoryIssueController {
    private final ResultService resultService;

    public CategoryIssueController(ResultService resultService) {
        this.resultService = resultService;
    }

    @Operation(summary = "Gets a page of saved findings for a report category",
            description = "Returns saved findings ordered by id. Stored category counts can include findings "
                    + "that were not saved or have been deleted; these are not available for drilldown.")
    @ApiResponse(responseCode = "200", description = "A page of findings, including empty records with paging metadata")
    @ApiResponse(responseCode = "400", description = "Invalid paging parameters",
            content = @Content(mediaType = "application/problem+json", schema = @Schema(implementation = ProblemDetail.class)))
    @ApiResponse(responseCode = "500", description = "Unexpected failure returned as Problem Details",
            content = @Content(mediaType = "application/problem+json", schema = @Schema(implementation = ProblemDetail.class)))
    @GetMapping("/{facilityId}/reports/{reportId}/categories/{categoryId}/issues")
    public CategoryIssuePage getIssues(@PathVariable String facilityId, @PathVariable String reportId,
            @PathVariable String categoryId,
            @Parameter(description = "One-based page number", example = "1")
            @RequestParam(defaultValue = "1") int pageNumber,
            @Parameter(description = "Number of records per page, from 1 to 100", example = "25")
            @RequestParam(defaultValue = "25") int pageSize) {
        if (pageNumber < 1 || pageSize < 1 || pageSize > 100) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST,
                    "pageNumber must be at least 1 and pageSize must be between 1 and 100");
        }
        if ((long) (pageNumber - 1) * pageSize > Integer.MAX_VALUE) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Requested page offset is too large");
        }
        return resultService.getCategoryIssuesPage(facilityId, reportId, categoryId, pageNumber, pageSize);
    }
}
