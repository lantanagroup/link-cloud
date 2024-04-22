package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.fhir.FhirHelper;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.hl7.fhir.r4.model.ImplementationGuide;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.utilities.npm.NpmPackage;
import org.springframework.stereotype.Service;
import org.yaml.snakeyaml.reader.StreamReader;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.List;

@Service
public class ArtifactService {
    private final ArtifactRepository repository;

    public ArtifactService(ArtifactRepository repository) {
        this.repository = repository;
    }

    public void createOrUpdateArtifact(Artifact.Types type, byte[] content) {
        String name = this.getArtifactName(type, content);
        List<Artifact> artifacts = this.repository.findByTypeAndName(type, name);

        if (artifacts.size() > 1) {
            throw new RuntimeException("Multiple artifacts found with the same name");
        } else if (artifacts.size() == 1) {
            Artifact artifact = artifacts.get(0);
            artifact.setContent(content);
            this.repository.save(artifact);
        } else {
            Artifact artifact = new Artifact();
            artifact.setType(type);
            artifact.setName(name);
            artifact.setContent(content);
            this.repository.save(artifact);
        }
    }

    public void deleteArtifact(Artifact.Types type, String name) {
        List<Artifact> artifacts = this.repository.findByTypeAndName(type, name);
        if (artifacts.size() > 1) {
            throw new RuntimeException("Multiple artifacts found with the same name");
        } else if (artifacts.size() == 1) {
            this.repository.delete(artifacts.get(0));
        }
    }

    public List<Artifact> listArtifacts() {
        return this.repository
                .findAll()
                .stream().map(artifact -> {
                    artifact.setContent(null);
                    return artifact;
                }).toList();
    }

    public List<Artifact> getArtifacts() {
        return this.repository.findAll();
    }

    private String getArtifactName(Artifact.Types type, byte[] content) {
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
                        throw new RuntimeException("Multiple ImplementationGuides found in package");
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
