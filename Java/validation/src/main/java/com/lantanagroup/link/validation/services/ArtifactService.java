package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.fhir.FhirHelper;
import com.lantanagroup.link.validation.entities.ArtifactEntity;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.hl7.fhir.r4.model.ImplementationGuide;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.utilities.npm.NpmPackage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.core.io.support.PathMatchingResourcePatternResolver;
import org.springframework.stereotype.Service;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.util.List;

@Service
public class ArtifactService {
    private static final Logger log = LoggerFactory.getLogger(ArtifactService.class);

    private final ArtifactRepository repository;

    public ArtifactService(ArtifactRepository repository) {
        this.repository = repository;

        this.initArtifacts(ArtifactEntity.Types.PACKAGE);
        this.initArtifacts(ArtifactEntity.Types.RESOURCE);
    }

    public void createOrUpdateArtifact(ArtifactEntity.Types type, byte[] content) {
        String name = this.getArtifactName(type, content);
        List<ArtifactEntity> artifactEntities = this.repository.findByTypeAndName(type, name);

        if (artifactEntities.size() > 1) {
            throw new RuntimeException("Multiple artifacts found with the same name");
        } else if (artifactEntities.size() == 1) {
            ArtifactEntity artifactEntity = artifactEntities.get(0);
            artifactEntity.setContent(content);
            this.repository.save(artifactEntity);
        } else {
            ArtifactEntity artifactEntity = new ArtifactEntity();
            artifactEntity.setType(type);
            artifactEntity.setName(name);
            artifactEntity.setContent(content);
            this.repository.save(artifactEntity);
        }
    }

    public void deleteArtifact(ArtifactEntity.Types type, String name) {
        List<ArtifactEntity> artifactEntities = this.repository.findByTypeAndName(type, name);
        if (artifactEntities.size() > 1) {
            throw new RuntimeException("Multiple artifacts found with the same name");
        } else if (artifactEntities.size() == 1) {
            this.repository.delete(artifactEntities.get(0));
        }
    }

    public List<ArtifactEntity> listArtifacts() {
        return this.repository
                .findAll()
                .stream().map(artifactEntity -> {
                    artifactEntity.setContent(null);
                    return artifactEntity;
                }).toList();
    }

    public List<ArtifactEntity> getArtifacts() {
        return this.repository.findAll();
    }

    private void initArtifacts(ArtifactEntity.Types type) {
        List<String> extensions = type == ArtifactEntity.Types.RESOURCE ? List.of("json", "xml") : List.of("tgz");
        String path = type == ArtifactEntity.Types.RESOURCE ? "classpath:/resources/**" : "classpath:/packages/**";
        PathMatchingResourcePatternResolver resolver = new PathMatchingResourcePatternResolver();
        org.springframework.core.io.Resource[] resources;

        try {
            resources = resolver.getResources(path);
        } catch (IOException e) {
            log.error("Error initializing artifacts for type {} from path {} in class resources", type, path, e);
            throw new RuntimeException("Error initializing artifacts");
        }

        for (org.springframework.core.io.Resource resourceResource : resources) {
            String extension = resourceResource.getFilename().substring(resourceResource.getFilename().lastIndexOf(".") + 1);
            File file;

            try {
                file = resourceResource.getFile();
            } catch (IOException e) {
                log.error("Error getting file for resource in class resources {}", resourceResource.getFilename(), e);
                continue;
            }

            if (file.isDirectory()) {
                continue;
            } else if (!extensions.contains(extension.toLowerCase())) {
                log.warn("Unexpected file name {} for type {} in class resources", resourceResource.getFilename(), type);
                continue;
            }

            log.info("Loading resource {}", resourceResource.getFilename());

            try {
                byte[] resourceContent = resourceResource.getContentAsByteArray();
                String resourceName = this.getArtifactName(type, resourceContent);

                if (this.repository.findByTypeAndName(type, resourceName).isEmpty()) {
                    ArtifactEntity artifactEntity = new ArtifactEntity();
                    artifactEntity.setType(type);
                    artifactEntity.setName(resourceName);
                    artifactEntity.setContent(resourceContent);
                    this.repository.save(artifactEntity);
                }
            } catch (IOException e) {
                log.error("Error get content for resource in class resources {}", resourceResource.getFilename(), e);
            }
        }
    }

    private String getArtifactName(ArtifactEntity.Types type, byte[] content) {
        switch (type) {
            case RESOURCE:
                String contentString = new String(content);
                Resource resource = FhirHelper.deserialize(contentString);
                if (resource != null) {
                    return resource.getIdElement().getIdPart();
                } else {
                    throw new RuntimeException("Invalid FHIR resource");
                }
            case PACKAGE:
                try {
                    NpmPackage npmPackage = NpmPackage.fromPackage(new ByteArrayInputStream(content));
                    List<String> igFileName = npmPackage.listResources("ImplementationGuide");

                    if (igFileName.isEmpty()) {
                        throw new RuntimeException("No ImplementationGuide found in package");
                    } else if (igFileName.size() > 1) {
                        log.warn("Multiple ImplementationGuides found in package, using the first one");
                    }

                    InputStream is = npmPackage.loadResource(igFileName.get(0));
                    ImplementationGuide ig = (ImplementationGuide) FhirHelper.deserialize(is);
                    return ig.getIdElement().getIdPart();
                } catch (IOException e) {
                    throw new RuntimeException("Failed to parse artifact package", e);
                }
            default:
                throw new RuntimeException("Unknown artifact type");
        }
    }
}
