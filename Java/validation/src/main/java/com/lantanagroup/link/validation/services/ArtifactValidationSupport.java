package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.ValidationSupportContext;
import ca.uhn.fhir.parser.IParser;
import ca.uhn.fhir.parser.LenientErrorHandler;
import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import org.hl7.fhir.common.hapi.validation.support.PrePopulatedValidationSupport;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.ImplementationGuide;
import org.hl7.fhir.r4.model.ResourceType;
import org.hl7.fhir.utilities.npm.NpmPackage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Collections;
import java.util.List;

public class ArtifactValidationSupport extends PrePopulatedValidationSupport {
    public static final Collection<ResourceType> SUPPORTED_RESOURCE_TYPES = List.of(
            ResourceType.ImplementationGuide,
            ResourceType.CodeSystem,
            ResourceType.ValueSet,
            ResourceType.StructureDefinition);
    private static final Logger logger = LoggerFactory.getLogger(ArtifactValidationSupport.class);

    private final List<ImplementationGuide> implementationGuides = new ArrayList<>();

    public ArtifactValidationSupport(FhirContext fhirContext) {
        super(fhirContext);
    }

    @Override
    public boolean isCodeSystemSupported(ValidationSupportContext validationSupportContext, String system) {
        return false;
    }

    @Override
    public boolean isValueSetSupported(ValidationSupportContext validationSupportContext, String url) {
        return false;
    }

    private IParser getParser() {
        IParser parser = getFhirContext().newJsonParser();
        parser.setParserErrorHandler(new LenientErrorHandler(false));
        return parser;
    }

    public void addArtifact(Artifact artifact) throws IOException {
        switch (artifact.getType()) {
            case PACKAGE -> addPackageArtifact(artifact);
            case RESOURCE -> addResourceArtifact(artifact);
        }
    }

    private void addPackageArtifact(Artifact artifact) throws IOException {
        if (artifact.getType() != ArtifactType.PACKAGE) {
            throw new IllegalArgumentException("Artifact is not a package");
        }
        logger.debug("Adding package artifact: {}", artifact.getName());
        NpmPackage npmPackage;
        try (InputStream stream = new ByteArrayInputStream(artifact.getContent())) {
            npmPackage = NpmPackage.fromPackage(stream);
        }
        for (ResourceType resourceType : SUPPORTED_RESOURCE_TYPES) {
            for (String file : npmPackage.listResources(resourceType.name())) {
                try (InputStream stream = npmPackage.loadResource(file)) {
                    addResource(stream);
                }
            }
        }
    }

    private void addResourceArtifact(Artifact artifact) throws IOException {
        if (artifact.getType() != ArtifactType.RESOURCE) {
            throw new IllegalArgumentException("Artifact is not a resource");
        }
        logger.debug("Adding resource artifact: {}", artifact.getName());
        try (InputStream stream = new ByteArrayInputStream(artifact.getContent())) {
            addResource(stream);
        }
    }

    private void addResource(InputStream stream) {
        IBaseResource resource = getParser().parseResource(stream);
        ResourceType resourceType = ResourceType.fromCode(resource.fhirType());
        if (!SUPPORTED_RESOURCE_TYPES.contains(resourceType)) {
            logger.warn("Unsupported resource type: {}", resourceType);
            return;
        }
        if (resource instanceof ImplementationGuide implementationGuide) {
            implementationGuides.add(implementationGuide);
        } else {
            addResource(resource);
        }
    }

    public List<ImplementationGuide> getImplementationGuides() {
        return Collections.unmodifiableList(implementationGuides);
    }
}
