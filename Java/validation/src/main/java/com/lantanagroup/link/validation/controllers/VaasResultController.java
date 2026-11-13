package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.RubricResultDto;
import com.lantanagroup.link.validation.services.RubricResultQueryService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.UUID;

@RestController
@RequestMapping("/api/validation/requests")
@RequiredArgsConstructor
@SecurityRequirement(name = "bearer-key")
@Tag(name = "Rubric Results", description = "Query persisted rubric evaluation results")
public class VaasResultController {

    private final RubricResultQueryService resultQueryService;

    @Operation(summary = "Fetch a persisted rubric result by request id")
    @GetMapping("/{requestId}")
    public ResponseEntity<RubricResultDto> getByRequestId(@PathVariable UUID requestId) {
        return resultQueryService.findByRequestId(requestId)
                .map(ResponseEntity::ok)
                .orElseGet(() -> ResponseEntity.notFound().build());
    }
}
