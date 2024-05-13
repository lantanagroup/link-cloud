package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.entities.ArtifactEntity;
import com.lantanagroup.link.validation.services.ArtifactService;
import io.swagger.v3.oas.annotations.Operation;
import org.springframework.web.bind.annotation.*;

import java.util.List;

@RestController
@RequestMapping("/api/artifact")
public class ArtifactController {
    private final ArtifactService artifactService;

    public ArtifactController(ArtifactService artifactService) {
        this.artifactService = artifactService;
    }

    @Operation(
            summary = "Create or update an artifact",
            description = "Create or update an artifact of the specified type with the specified content",
            tags = {"Artifacts"},
            operationId = "createOrUpdateArtifact"
    )
    @PutMapping("/{type}")
    public void createOrUpdateArtifact(@PathVariable("type") ArtifactEntity.Types type, @RequestParam String name, @RequestBody byte[] content) {
        this.artifactService.createOrUpdateArtifact(name, type, content);
    }

    @Operation(
            summary = "Delete an artifact",
            description = "Delete an artifact of the specified type with the specified name",
            tags = {"Artifacts"},
            operationId = "deleteArtifact"
    )
    @DeleteMapping("/{type}/{name}")
    public void deleteArtifact(@PathVariable("type") ArtifactEntity.Types type, @PathVariable("name") String name) {
        this.artifactService.deleteArtifact(type, name);
    }

    @Operation(
            summary = "List artifacts",
            description = "List all artifacts excluding their content",
            tags = {"Artifacts"},
            operationId = "listArtifacts"
    )
    @GetMapping
    public List<ArtifactEntity> listArtifacts() {
        return this.artifactService.listArtifacts();
    }
}
