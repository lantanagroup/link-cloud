package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import ca.uhn.fhir.rest.api.SummaryEnum;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.models.TerminologyDependency;
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
import java.util.*;

@Service
public class ArtifactService {
    private static final Logger logger = LoggerFactory.getLogger(ArtifactService.class);

    private final FhirContext fhirContext;
    private final ArtifactRepository artifactRepository;
    private final LinkConfig linkConfig;
    private ArtifactValidationSupport validationSupport;

    public ArtifactService(FhirContext fhirContext, ArtifactRepository artifactRepository, LinkConfig linkConfig) {
        this.fhirContext = fhirContext;
        this.artifactRepository = artifactRepository;
        this.linkConfig = linkConfig;
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

    public List<TerminologyDependency> getTerminologyDependencies() throws IOException {
        return getTerminologyDependencies(null);
    }

    public List<TerminologyDependency> getTerminologyDependencies(String packageId) throws IOException {
        ArtifactValidationSupport support = getValidationSupport();
        List<IBaseResource> resources;

        if (StringUtils.isNotEmpty(packageId)) {
            Artifact artifact = artifactRepository.findByTypeAndName(ArtifactType.PACKAGE, packageId).orElse(null);
            if (artifact == null) {
                return Collections.emptyList();
            }
            ArtifactValidationSupport packageSupport = new ArtifactValidationSupport(fhirContext);
            packageSupport.addArtifact(artifact);
            resources = packageSupport.fetchAllStructureDefinitions();
        } else {
            resources = support.fetchAllStructureDefinitions();
        }

        Set<String> dependencyStrings = new HashSet<>();
        if (resources != null) {
            for (IBaseResource resource : resources) {
                if (resource instanceof StructureDefinition sd) {
                    if (sd.hasSnapshot()) {
                        for (ElementDefinition ed : sd.getSnapshot().getElement()) {
                            addBindingDependencies(ed, dependencyStrings);
                            addFixedPatternDependencies(ed, dependencyStrings);
                        }
                    }
                    if (sd.hasDifferential()) {
                        for (ElementDefinition ed : sd.getDifferential().getElement()) {
                            addBindingDependencies(ed, dependencyStrings);
                            addFixedPatternDependencies(ed, dependencyStrings);
                        }
                    }
                }
            }
        }

        // Remove tx dependencies related to Core FHIR R4, that start with either
        // http://hl7.org/fhir/ValueSet
        // or http://hl7.org/fhir/CodeSystem
        dependencyStrings.removeIf(s ->
                s.startsWith("http://hl7.org/fhir/ValueSet") ||
                s.startsWith("http://hl7.org/fhir/CodeSystem") ||
                s.startsWith("http://terminology.hl7.org/ValueSet") ||
                s.startsWith("http://terminology.hl7.org/CodeSystem"));

        Map<String, Set<String>> existingResources = new HashMap<>();
        String remoteUrl = getTerminologyBaseUrl();
        if (StringUtils.isNotEmpty(remoteUrl)) {
            fetchRemoteTerminology(remoteUrl, existingResources);
        } else {
            fetchLocalTerminology(support, existingResources);
        }

        List<TerminologyDependency> results = new ArrayList<>();
        for (String depString : dependencyStrings) {
            String url;
            String version = null;
            if (depString.contains("|")) {
                url = depString.substring(0, depString.indexOf("|"));
                version = depString.substring(depString.indexOf("|") + 1);
            } else {
                url = depString;
            }

            TerminologyDependency dep = new TerminologyDependency();
            dep.setUrl(url);
            dep.setVersion(version);
            boolean resourceExists = existingResources.containsKey(url);
            dep.setResourceExists(resourceExists);
            if (version == null) {
                dep.setVersionExists(true);
            } else {
                dep.setVersionExists(resourceExists && existingResources.get(url).contains(version));
            }
            results.add(dep);
        }
        return results;
    }

    private String getTerminologyBaseUrl() {
        if (StringUtils.isNotEmpty(linkConfig.getFhirTerminologyServiceUrl())) {
            return linkConfig.getFhirTerminologyServiceUrl();
        } else if (StringUtils.isNotEmpty(linkConfig.getTerminologyServiceUrl())) {
            String baseUrl = linkConfig.getTerminologyServiceUrl();
            if (!baseUrl.endsWith("/")) {
                baseUrl += "/";
            }
            return baseUrl + "api/terminology/fhir";
        }
        return null;
    }

    private void fetchRemoteTerminology(String baseUrl, Map<String, Set<String>> existingResources) {
        IGenericClient client = fhirContext.newRestfulGenericClient(baseUrl);
        try {
            Bundle vsBundle = client.search()
                    .forResource(ValueSet.class)
                    .summaryMode(SummaryEnum.TRUE)
                    .returnBundle(Bundle.class)
                    .execute();
            processBundle(vsBundle, existingResources, client);
        } catch (Exception e) {
            logger.error("Failed to fetch ValueSets from remote terminology service", e);
        }

        try {
            Bundle csBundle = client.search()
                    .forResource(CodeSystem.class)
                    .summaryMode(SummaryEnum.TRUE)
                    .returnBundle(Bundle.class)
                    .execute();
            processBundle(csBundle, existingResources, client);
        } catch (Exception e) {
            logger.error("Failed to fetch CodeSystems from remote terminology service", e);
        }
    }

    private void processBundle(Bundle bundle, Map<String, Set<String>> existingResources, IGenericClient client) {
        while (bundle != null) {
            for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
                IBaseResource resource = entry.getResource();
                String url = null;
                String version = null;
                if (resource instanceof ValueSet vs) {
                    url = vs.getUrl();
                    version = vs.getVersion();
                } else if (resource instanceof CodeSystem cs) {
                    url = cs.getUrl();
                    version = cs.getVersion();
                }
                if (url != null) {
                    existingResources.computeIfAbsent(url, k -> new HashSet<>()).add(version);
                }
            }
            if (bundle.getLink(Bundle.LINK_NEXT) != null) {
                bundle = client.loadPage().next(bundle).execute();
            } else {
                bundle = null;
            }
        }
    }

    private void fetchLocalTerminology(ArtifactValidationSupport support, Map<String, Set<String>> existingResources) {
        List<IBaseResource> resources = support.fetchAllConformanceResources();
        for (IBaseResource resource : resources) {
            String url = null;
            String version = null;
            if (resource instanceof ValueSet vs) {
                url = vs.getUrl();
                version = vs.getVersion();
            } else if (resource instanceof CodeSystem cs) {
                url = cs.getUrl();
                version = cs.getVersion();
            }
            if (url != null) {
                existingResources.computeIfAbsent(url, k -> new HashSet<>()).add(version);
            }
        }
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
