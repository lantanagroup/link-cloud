package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.api.SummaryEnum;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.models.TerminologyDependency;
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

        Map<String, TerminologyDependency> dependencyMap = new HashMap<>();
        if (resources != null) {
            for (IBaseResource resource : resources) {
                if (resource instanceof StructureDefinition sd) {
                    if (sd.hasSnapshot()) {
                        for (ElementDefinition ed : sd.getSnapshot().getElement()) {
                            addBindingDependencies(sd, ed, dependencyMap);
                            //addFixedPatternDependencies(sd, ed, dependencyMap);
                        }
                    } else if (sd.hasDifferential()) {
                        for (ElementDefinition ed : sd.getDifferential().getElement()) {
                            addBindingDependencies(sd, ed, dependencyMap);
                            addFixedPatternDependencies(sd, ed, dependencyMap);
                        }
                    }
                }
            }
        }

        // Remove tx dependencies related to Core FHIR R4, that start with either
        // http://hl7.org/fhir/ValueSet
        // or http://hl7.org/fhir/CodeSystem
        dependencyMap.keySet().removeIf(s ->
                s.startsWith("http://hl7.org/fhir/ValueSet") ||
                s.startsWith("http://hl7.org/fhir/CodeSystem") ||
                s.startsWith("http://terminology.hl7.org/ValueSet") ||
                s.startsWith("http://terminology.hl7.org/CodeSystem"));

        Map<String, Set<String>> existingResources = new HashMap<>();
        String remoteUrl = getTerminologyBaseUrl();
        if (StringUtils.isNotEmpty(remoteUrl)) {
            logger.debug("Fetching terminology from remote server: {}", remoteUrl);
            fetchRemoteTerminology(remoteUrl, existingResources);
        } else {
            logger.debug("Fetching terminology from local server");
            fetchLocalTerminology(support, existingResources);
        }

        for (TerminologyDependency dep : dependencyMap.values()) {
            String url = dep.getUrl();
            String version = dep.getVersion();
            boolean resourceExists = existingResources.containsKey(url);
            dep.setResourceExists(resourceExists);
            if (version == null) {
                dep.setVersionExists(true);
            } else {
                dep.setVersionExists(resourceExists && existingResources.get(url).contains(version));
            }
        }

        List<TerminologyDependency> results = new ArrayList<>(dependencyMap.values());
        results.sort(Comparator.comparing(TerminologyDependency::getUrl));
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
            logger.debug("Fetching ValueSets and CodeSystems from remote terminology service");
            Bundle vsBundle = client.search()
                    .forResource(ValueSet.class)
                    .summaryMode(SummaryEnum.TRUE)
                    .returnBundle(Bundle.class)
                    .execute();
            logger.debug("Fetched {} ValueSets from remote terminology service", vsBundle.getTotal());
            processBundle(vsBundle, existingResources, client);
        } catch (Exception e) {
            logger.error("Failed to fetch ValueSets from remote terminology service", e);
        }

        try {
            logger.debug("Fetching CodeSystems from remote terminology service");
            Bundle csBundle = client.search()
                    .forResource(CodeSystem.class)
                    .summaryMode(SummaryEnum.TRUE)
                    .returnBundle(Bundle.class)
                    .execute();
            logger.debug("Fetched {} CodeSystems from remote terminology service", csBundle.getTotal());
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
        assert resources != null;
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

    private void addBindingDependencies(StructureDefinition sd, ElementDefinition ed, Map<String, TerminologyDependency> dependencyMap) {
        if (ed.hasBinding() && ed.getBinding().hasValueSet()) {
            addDependency(ed.getBinding().getValueSet(), sd, ed, dependencyMap);
        }
    }

    private void addFixedPatternDependencies(StructureDefinition sd, ElementDefinition ed, Map<String, TerminologyDependency> dependencyMap) {
        if (ed.hasFixed()) {
            addTypeDependencies(sd, ed, ed.getFixed(), dependencyMap);
        }
        if (ed.hasPattern()) {
            addTypeDependencies(sd, ed, ed.getPattern(), dependencyMap);
        }
    }

    private void addTypeDependencies(StructureDefinition sd, ElementDefinition ed, Type type, Map<String, TerminologyDependency> dependencyMap) {
        if (type instanceof Coding coding) {
            if (coding.hasSystem()) {
                String dependency = coding.getSystem();
                if (coding.hasVersion()) {
                    dependency += "|" + coding.getVersion();
                }
                addDependency(dependency, sd, ed, dependencyMap);
            }
        } else if (type instanceof CodeableConcept codeableConcept) {
            for (Coding coding : codeableConcept.getCoding()) {
                addTypeDependencies(sd, ed, coding, dependencyMap);
            }
        }
    }

    private void addDependency(String depString, StructureDefinition sd, ElementDefinition ed, Map<String, TerminologyDependency> dependencyMap) {
        TerminologyDependency dep = dependencyMap.computeIfAbsent(depString, k -> {
            TerminologyDependency d = new TerminologyDependency();
            String url;
            String version = null;
            if (k.contains("|")) {
                url = k.substring(0, k.indexOf("|"));
                version = k.substring(k.indexOf("|") + 1);
            } else {
                url = k;
            }
            d.setUrl(url);
            d.setVersion(version);
            return d;
        });

        String profileUrl = sd.getUrl();
        String elementPath = ed.getPath();

        TerminologyDependency.SourceProfile sp = dep.getSourceProfile().stream()
                .filter(s -> s.getUrl().equals(profileUrl))
                .findFirst()
                .orElseGet(() -> {
                    TerminologyDependency.SourceProfile s = new TerminologyDependency.SourceProfile();
                    s.setUrl(profileUrl);
                    dep.getSourceProfile().add(s);
                    return s;
                });

        if (!sp.getElement().contains(elementPath)) {
            sp.getElement().add(elementPath);
        }
    }
}
