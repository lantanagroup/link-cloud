package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.apache.commons.io.FilenameUtils;
import org.apache.commons.lang3.StringUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.*;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.core.io.Resource;
import org.springframework.core.io.support.PathMatchingResourcePatternResolver;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

@Service
public class ArtifactService {
    private static final Logger logger = LoggerFactory.getLogger(ArtifactService.class);

    private final FhirContext fhirContext;
    private final ArtifactRepository artifactRepository;
    private ArtifactValidationSupport validationSupport;

    public ArtifactService(FhirContext fhirContext, ArtifactRepository artifactRepository) {
        this.fhirContext = fhirContext;
        this.artifactRepository = artifactRepository;
    }

    private void doSaveArtifact(ArtifactType type, String name, byte[] content) {
        Artifact artifact = artifactRepository.findByTypeAndName(type, name).orElseGet(Artifact::new);
        artifact.setType(type);
        artifact.setName(name);
        artifact.setContent(content);
        artifactRepository.save(artifact);
    }

    public void saveArtifact(ArtifactType type, String name, byte[] content) {
        doSaveArtifact(type, name, content);
        invalidateValidationSupport();
    }

    public void deleteArtifact(ArtifactType type, String name) {
        if (artifactRepository.deleteByTypeAndName(type, name)) {
            invalidateValidationSupport();
        }
    }

    public void initializeArtifacts() throws IOException {
        logger.info("Initializing artifacts");
        initializeArtifacts(ArtifactType.PACKAGE, "classpath*:artifacts/packages/*.tgz");
        initializeArtifacts(ArtifactType.RESOURCE, "classpath*:artifacts/resources/*.json");
        invalidateValidationSupport();
    }

    private void initializeArtifacts(ArtifactType type, String locationPattern) throws IOException {
        PathMatchingResourcePatternResolver resolver = new PathMatchingResourcePatternResolver();
        for (Resource resource : resolver.getResources(locationPattern)) {
            String name = FilenameUtils.getBaseName(resource.getFilename());
            if (StringUtils.isEmpty(name)) {
                logger.warn("Empty filename: {}", resource.getDescription());
                continue;
            }
            logger.debug("Initializing {} artifact: {}", type, name);
            doSaveArtifact(type, name, resource.getContentAsByteArray());
        }
    }

    private synchronized void invalidateValidationSupport() {
        validationSupport = null;
    }

    public synchronized ArtifactValidationSupport getValidationSupport() throws IOException {
        if (validationSupport == null) {
            validationSupport = new ArtifactValidationSupport(fhirContext);
            for (Artifact artifact : artifactRepository.findAll()) {
                validationSupport.addArtifact(artifact);
            }
        }
        return validationSupport;
    }

    public Set<String> getTerminologyDependencies() throws IOException {
        ArtifactValidationSupport support = getValidationSupport();
        List<IBaseResource> resources = support.fetchAllStructureDefinitions();
        Set<String> dependencies = new HashSet<>();
        if (resources == null) {
            return dependencies;
        }
        for (IBaseResource resource : resources) {
            if (resource instanceof StructureDefinition sd) {
                if (sd.hasSnapshot()) {
                    for (ElementDefinition ed : sd.getSnapshot().getElement()) {
                        addBindingDependencies(ed, dependencies);
                        addFixedPatternDependencies(ed, dependencies);
                    }
                }
                if (sd.hasDifferential()) {
                    for (ElementDefinition ed : sd.getDifferential().getElement()) {
                        addBindingDependencies(ed, dependencies);
                        addFixedPatternDependencies(ed, dependencies);
                    }
                }
            }
        }
        return dependencies;
    }

    private void addBindingDependencies(ElementDefinition ed, Set<String> dependencies) {
        if (ed.hasBinding() && ed.getBinding().hasValueSet()) {
            dependencies.add(ed.getBinding().getValueSet());
        }
    }

    private void addFixedPatternDependencies(ElementDefinition ed, Set<String> dependencies) {
        if (ed.hasFixed()) {
            addTypeDependencies(ed.getFixed(), dependencies);
        }
        if (ed.hasPattern()) {
            addTypeDependencies(ed.getPattern(), dependencies);
        }
    }

    private void addTypeDependencies(Type type, Set<String> dependencies) {
        if (type instanceof Coding coding) {
            if (coding.hasSystem()) {
                String dependency = coding.getSystem();
                if (coding.hasVersion()) {
                    dependency += "|" + coding.getVersion();
                }
                dependencies.add(dependency);
            }
        } else if (type instanceof CodeableConcept codeableConcept) {
            for (Coding coding : codeableConcept.getCoding()) {
                addTypeDependencies(coding, dependencies);
            }
        }
    }
}
