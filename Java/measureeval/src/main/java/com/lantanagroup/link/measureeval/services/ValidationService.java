package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.util.BundleUtil;
import ca.uhn.fhir.util.TerserUtil;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.ValidationOptions;
import com.lantanagroup.link.measureeval.utils.LinkValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.CommonCodeSystemsTerminologyService;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.SnapshotGeneratingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.DomainResource;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.utilities.npm.NpmPackage;

import java.io.IOException;
import java.util.HashSet;
import java.util.List;
import java.util.UUID;

public class ValidationService {

    public static final String VALIDATION_RESULT_EXT_URL =
            "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/validation-result";

    private final FhirContext fhirContext =  FhirContext.forR4();
    private final LinkValidationSupport lvs;
    private final FhirValidator validator;
    private final boolean truncateToMustSupport;

    public ValidationService(boolean truncateToMustSupport) {
        this(List.of(), List.of(), truncateToMustSupport);
    }

    public ValidationService(List<String> urls, List<String> directories, boolean truncateToMustSupport) {
        this.truncateToMustSupport = truncateToMustSupport;
        try {
            this.lvs = new LinkValidationSupport(fhirContext);
            for (var url : urls) {
                lvs.loadPackage(NpmPackage.fromUrl(url));
            }
            for (var dir : directories) {
                lvs.loadPackage(NpmPackage.fromFolder(dir));
            }
            var vsc = new ValidationSupportChain(
                    lvs,
                    new CommonCodeSystemsTerminologyService(fhirContext),
                    new DefaultProfileValidationSupport(fhirContext),
//                    new RemoteTerminologyServiceValidationSupport(fhirContext, "http://tx.fhir.org/r4"),
                    new InMemoryTerminologyServerValidationSupport(fhirContext),
                    new SnapshotGeneratingValidationSupport(fhirContext)
            );
            validator = new FhirValidator(fhirContext);
            FhirInstanceValidator instanceValidator = new FhirInstanceValidator(vsc);
            validator.registerValidatorModule(instanceValidator);
        } catch (IOException e) {
            throw new RuntimeException(e);
        }
    }

    public void validate(Bundle bundle) {
        for (var resource : BundleUtil.toListOfResources(fhirContext, bundle)) {
            var res = (DomainResource) resource;
            var profiles = lvs.getProfileMap().get(res.fhirType());
            validate(res, profiles);
        }
    }

    public DomainResource validate(DomainResource resource, List<String> profiles) {
        var options = new ValidationOptions();
        profiles.forEach(options::addProfile);

        if (truncateToMustSupport) {
            resource = onlyIncludeMustSupport(resource, profiles);
        }

        var result = validator.validateWithResult(resource, options);
        if (result.isSuccessful()) {
            for  (var p : profiles) {
                if (!resource.getMeta().hasProfile(p)) resource.getMeta().addProfile(p);
            }
        } else {
            var id = UUID.randomUUID().toString();
            var oo = result.toOperationOutcome();
            oo.setId(id);
            resource.addContained((Resource) oo);
            resource.addExtension(
                    VALIDATION_RESULT_EXT_URL,
                    new Reference("#" + id));
        }

        return resource;
    }

    public DomainResource onlyIncludeMustSupport(DomainResource resource, List<String> profiles) {
        var elementsToKeep = new HashSet<String>();
        for (var profile : profiles) {
            elementsToKeep.addAll(lvs.getMustSupportElements().get(profile));
        }
        for (var element : fhirContext.getResourceDefinition(resource).getChildren()) {
            if (!elementsToKeep.contains(element.getElementName())) {
                TerserUtil.clearField(fhirContext, resource, element.getElementName());
            }
        }
        return resource;
    }
}
