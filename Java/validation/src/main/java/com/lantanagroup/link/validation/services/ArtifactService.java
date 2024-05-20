package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.config.ArtifactConfig;
import com.lantanagroup.link.validation.entities.ArtifactEntity;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.apache.commons.lang3.StringUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.core.io.support.PathMatchingResourcePatternResolver;
import org.springframework.stereotype.Service;

import java.io.File;
import java.io.IOException;
import java.util.List;
import java.util.Objects;

@Service
public class ArtifactService {
    private static final Logger log = LoggerFactory.getLogger(ArtifactService.class);

    private final ArtifactRepository repository;

    public ArtifactService(ArtifactRepository repository, ArtifactConfig artifactConfig) {
        this.repository = repository;

        if (artifactConfig.isInit()) {
            this.initArtifacts(ArtifactEntity.Types.PACKAGE);
            this.initArtifacts(ArtifactEntity.Types.RESOURCE);
        } else {
            log.info("Skipping artifact initialization due to configuration");
        }
    }

    public void createOrUpdateArtifact(String name, ArtifactEntity.Types type, byte[] content) {
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
            String extension = Objects.requireNonNull(resourceResource.getFilename())
                    .substring(resourceResource.getFilename().lastIndexOf(".") + 1)
                    .toLowerCase();
            String fileName = new File(resourceResource.getFilename())
                    .getName();

            if (StringUtils.isEmpty(fileName) || StringUtils.isEmpty(extension)) {
                continue;
            } else if (!extensions.contains(extension.toLowerCase())) {
                log.warn("Unexpected file name {} for type {} in class resources", resourceResource.getFilename(), type);
                continue;
            }

            // Remove the file extension and remove the "CodeSystem-" or "ValueSet-" prefix
            fileName = fileName.substring(0, resourceResource.getFilename().lastIndexOf("."))
                    .replaceFirst("^(CodeSystem|ValueSet)-", "");

            log.info("Loading resource {}", resourceResource.getFilename());

            try {
                byte[] resourceContent = resourceResource.getContentAsByteArray();

                if (this.repository.findByTypeAndName(type, fileName).isEmpty()) {
                    ArtifactEntity artifactEntity = new ArtifactEntity();
                    artifactEntity.setType(type);
                    artifactEntity.setName(fileName);
                    artifactEntity.setContent(resourceContent);
                    this.repository.save(artifactEntity);
                }
            } catch (IOException e) {
                log.error("Error get content for resource in class resources {}", resourceResource.getFilename(), e);
            }
        }
    }
}
