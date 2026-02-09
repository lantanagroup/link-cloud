package com.lantanagroup.link.validation.controllers;

import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.shared.serdes.Views;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import com.lantanagroup.link.validation.services.ArtifactService;
import com.lantanagroup.link.validation.models.PackageDetailsModel;
import com.lantanagroup.link.validation.models.TerminologyDependency;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import jakarta.transaction.Transactional;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;
import org.springframework.web.server.ServerErrorException;

import java.io.IOException;
import java.util.List;
import java.util.Set;

@RestController
@RequestMapping("/api/validation/artifact")
@SecurityRequirement(name = "bearer-key")
@Transactional
public class ArtifactController {
    private final ArtifactRepository artifactRepository;
    private final ArtifactService artifactService;

    public ArtifactController(ArtifactRepository artifactRepository, ArtifactService artifactService) {
        this.artifactRepository = artifactRepository;
        this.artifactService = artifactService;
    }

    @Operation(summary = "Creates or updates artifacts using classpath resources")
    @PostMapping("/$initialize")
    public void initializeArtifacts() {
        try {
            artifactService.initializeArtifacts();
        } catch (Exception e) {
            throw new ServerErrorException("Failed to initialize artifacts", e);
        }
    }

    @Operation(summary = "Gets terminology dependencies from all artifacts")
    @GetMapping("/tx-dependencies")
    public List<TerminologyDependency> getTerminologyDependencies() {
        try {
            return artifactService.getTerminologyDependencies();
        } catch (IOException e) {
            throw new ServerErrorException("Failed to get terminology dependencies", e);
        }
    }

    @Operation(summary = "Gets terminology dependencies from a specific package")
    @GetMapping("/PACKAGE/{packageId}/tx-dependencies")
    public List<TerminologyDependency> getTerminologyDependencies(@PathVariable String packageId) {
        if (artifactRepository.findByTypeAndName(ArtifactType.PACKAGE, packageId).isEmpty()) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "Package not found");
        }
        try {
            return artifactService.getTerminologyDependencies(packageId);
        } catch (IOException e) {
            throw new ServerErrorException("Failed to get terminology dependencies for package: " + packageId, e);
        }
    }

    @Operation(summary = "Gets details of a specific package")
    @GetMapping("/PACKAGE/{packageId}")
    public PackageDetailsModel getPackageDetails(@PathVariable String packageId) {
        try {
            PackageDetailsModel model = artifactService.getPackageDetails(packageId);
            if (model == null) {
                throw new ResponseStatusException(HttpStatus.NOT_FOUND, "Package not found");
            }
            return model;
        } catch (IOException e) {
            throw new ServerErrorException("Failed to get package details for package: " + packageId, e);
        }
    }

    @Operation(summary = "Gets all artifacts")
    @GetMapping
    @JsonView(Views.Summary.class)
    public List<Artifact> getArtifacts() {
        return artifactRepository.findAll();
    }

    @Operation(summary = "Gets an artifact")
    @GetMapping("/{type}/{name}")
    @JsonView(Views.Detail.class)
    public Artifact getArtifact(@PathVariable ArtifactType type, @PathVariable String name) {
        return artifactRepository.findByTypeAndName(type, name)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "Artifact not found"));
    }

    @Operation(summary = "Gets the content of an artifact")
    @GetMapping("/{type}/{name}/content")
    public byte[] getArtifactContent(@PathVariable ArtifactType type, @PathVariable String name) {
        return getArtifact(type, name).getContent();
    }

    @Operation(summary = "Creates or updates an artifact")
    @PutMapping("/{type}/{name}")
    public void saveArtifact(@PathVariable ArtifactType type, @PathVariable String name, @RequestBody byte[] content) {
        artifactService.saveArtifact(type, name, content);
    }

    @Operation(summary = "Deletes an artifact")
    @DeleteMapping("/{type}/{name}")
    public void deleteArtifact(@PathVariable ArtifactType type, @PathVariable String name) {
        artifactService.deleteArtifact(type, name);
    }
}
