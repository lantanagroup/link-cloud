package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.validation.IValidationContext;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.instance.model.api.IBaseResource;

/**
 * A {@link FhirInstanceValidator} that emits {@link ProfileAttributedValidationMessage}s in place of HAPI's plain
 * {@code SingleValidationMessage}s. HAPI's concurrent bundle validation prefixes each message's location in place,
 * so these instances (and their profiles) come back out of {@code FhirValidator.validateWithResult}. See
 * LEGLINK-1172.
 */
public class ProfileCapturingInstanceValidator extends FhirInstanceValidator {
    public ProfileCapturingInstanceValidator(IValidationSupport validationSupport) {
        super(validationSupport);
    }

    @Override
    public void validateResource(IValidationContext<IBaseResource> validationContext) {
        validate(validationContext).stream()
                .map(ProfileAttributedValidationMessage::create)
                .forEachOrdered(validationContext::addValidationMessage);
    }
}
