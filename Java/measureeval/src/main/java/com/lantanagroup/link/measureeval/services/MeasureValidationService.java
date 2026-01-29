package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.models.RelatedArtifactInfo;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.RelatedArtifact;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.web.server.ResponseStatusException;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

@Service
public class MeasureValidationService {
    private static final Logger logger = LoggerFactory.getLogger(MeasureValidationService.class);
    private static final List<String> IGNORED_ARTIFACT_URL_PREFIXES = List.of(
            "http://fhir.org/guides/cqf/"
    );
    private final MeasureDefinitionRepository repository;

    public MeasureValidationService(MeasureDefinitionRepository repository) {
        this.repository = repository;
    }

    /**
     * Retrieves related artifact information for a measure, indicating if each artifact is present in the
     * measure definition, and where the artifact is used by other libraries.
     */
    public List<RelatedArtifactInfo> getRelatedArtifacts(String id) {
        MeasureDefinition measureDefinition = repository.findById(id)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "Measure definition not found"));

        Bundle bundle = measureDefinition.getBundle();
        if (bundle == null) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Measure definition does not contain a bundle");
        }

        Set<String> bundleResourceUrls = new HashSet<>();

        // Collects URLs of resources within the bundle
        for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
            Resource resource = entry.getResource();
            if (resource == null) continue;

            // Extract canonical URL if available
            String url = getResourceUrl(resource);
            if (url != null) {
                bundleResourceUrls.add(url);
            }
        }

        logger.debug("Found {} resources in measure bundle", bundleResourceUrls.size());

        List<RelatedArtifactInfo> relatedArtifactInfos = new ArrayList<>();

        for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
            if (entry.getResource() instanceof Library library) {
                for (RelatedArtifact relatedArtifact : library.getRelatedArtifact()) {
                    // Processes related artifacts to extract dependency information
                    if (relatedArtifact.getType() == RelatedArtifact.RelatedArtifactType.DEPENDSON) {
                        String artifactUrl = relatedArtifact.getResource();
                        if (artifactUrl == null || artifactUrl.isEmpty()) {
                            continue;
                        }

                        // Skip artifacts with ignored URL prefixes
                        if (IGNORED_ARTIFACT_URL_PREFIXES.stream().anyMatch(artifactUrl::startsWith)) {
                            continue;
                        }

                        // Extract version from URL if present (format: url|version)
                        String baseUrl = artifactUrl;
                        String version = null;
                        if (artifactUrl.contains("|")) {
                            String[] parts = artifactUrl.split("\\|", 2);
                            baseUrl = parts[0];
                            version = parts[1];
                        }

                        // Check if the artifact exists in the bundle
                        boolean found = bundleResourceUrls.contains(artifactUrl);

                        String finalBaseUrl = baseUrl;
                        String finalVersion = version;
                        // Finds or creates artifact info; populates if needed
                        RelatedArtifactInfo info = relatedArtifactInfos.stream()
                                .filter(i -> finalBaseUrl.equals(i.getUrl()))
                                .findFirst()
                                .orElseGet(() -> {
                                    RelatedArtifactInfo newInfo = new RelatedArtifactInfo();
                                    newInfo.setName(relatedArtifact.getDisplay());
                                    newInfo.setUrl(finalBaseUrl);
                                    newInfo.setVersion(finalVersion);
                                    newInfo.setFound(found);
                                    relatedArtifactInfos.add(newInfo);
                                    return newInfo;
                                });

                        String libUrl = library.getUrl();
                        String libName = library.getName();
                        if (libUrl != null) {
                            info.getLibraries().put(libUrl, libName);
                        }
                    }
                }
            }
        }

        logger.debug("Found {} related artifacts in measure definition {}", relatedArtifactInfos.size(), id);

        int notFoundCount = relatedArtifactInfos.stream()
                .filter(info -> !info.isFound())
                .mapToInt(info -> 1)
                .sum();

        logger.debug("Found {} artifacts not present in measure bundle {}", notFoundCount, id);

        return relatedArtifactInfos;
    }

    /**
     * Extracts resource URL, appending version if present
     */
    private String getResourceUrl(Resource resource) {
        if (resource instanceof org.hl7.fhir.r4.model.MetadataResource metadataResource) {
            String url = metadataResource.getUrl();
            String version = metadataResource.getVersion();
            if (url != null && version != null && !version.isEmpty()) {
                return url + "|" + version;
            }
            return url;
        }
        return null;
    }
}
