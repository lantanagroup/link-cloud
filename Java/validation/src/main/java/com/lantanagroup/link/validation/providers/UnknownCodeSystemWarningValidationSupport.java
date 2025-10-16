package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.ConceptValidationOptions;
import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.context.support.LookupCodeRequest;
import ca.uhn.fhir.context.support.ValidationSupportContext;
import jakarta.annotation.Nonnull;
import jakarta.annotation.Nullable;
import org.apache.commons.lang3.Validate;
import org.hl7.fhir.common.hapi.validation.support.BaseValidationSupport;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.ArrayList;
import java.util.List;

public class UnknownCodeSystemWarningValidationSupport extends BaseValidationSupport {
    private static final Logger ourLog = LoggerFactory.getLogger(UnknownCodeSystemWarningValidationSupport.class);
    public static final IValidationSupport.IssueSeverity DEFAULT_SEVERITY;
    private IValidationSupport.IssueSeverity myNonExistentCodeSystemSeverity;
    private List<String> whiteListCodeSystemRegex = new ArrayList<>();

    public UnknownCodeSystemWarningValidationSupport(FhirContext theFhirContext, List<String> whiteListCodeSystemRegex) {
        super(theFhirContext);
        this.myNonExistentCodeSystemSeverity = DEFAULT_SEVERITY;
        this.whiteListCodeSystemRegex = whiteListCodeSystemRegex;
    }

    public String getName() {
        return this.getFhirContext().getVersion().getVersion() + " Unknown Code System Warning Validation Support";
    }

    public boolean isValueSetSupported(ValidationSupportContext theValidationSupportContext, String theValueSetUrl) {
        return true;
    }

    public boolean isCodeSystemSupported(ValidationSupportContext theValidationSupportContext, String theSystem) {
        return this.canValidateCodeSystem(theValidationSupportContext, theSystem);
    }

    @Nullable
    public IValidationSupport.LookupCodeResult lookupCode(ValidationSupportContext theValidationSupportContext, @Nonnull LookupCodeRequest theLookupCodeRequest) {
        if (this.whiteListCodeSystemRegex.stream().anyMatch(regex -> theLookupCodeRequest.getSystem().matches(regex))) {
            return (new IValidationSupport.LookupCodeResult()).setFound(true);
        }

        return this.canValidateCodeSystem(theValidationSupportContext, theLookupCodeRequest.getSystem()) ? (new IValidationSupport.LookupCodeResult()).setFound(true) : null;
    }

    public IValidationSupport.CodeValidationResult validateCode(@Nonnull ValidationSupportContext theValidationSupportContext, @Nonnull ConceptValidationOptions theOptions, String theCodeSystem, String theCode, String theDisplay, String theValueSetUrl) {
        if (!this.canValidateCodeSystem(theValidationSupportContext, theCodeSystem)) {
            return null;
        } else {
            if (this.whiteListCodeSystemRegex.stream().anyMatch(theCodeSystem::matches)) {
                return null;
            }

            IValidationSupport.CodeValidationResult result = new IValidationSupport.CodeValidationResult();
            result.setSeverity(this.myNonExistentCodeSystemSeverity);
            String theMessage = "CodeSystem is unknown and can't be validated: " + theCodeSystem + " for '" + theCodeSystem + "#" + theCode + "'";
            result.setMessage(theMessage);
            if (this.myNonExistentCodeSystemSeverity == IssueSeverity.INFORMATION) {
                result.setCode(theCode);
                result.setSeverity((IValidationSupport.IssueSeverity)null);
                result.setMessage((String)null);
            } else {
                result.addCodeValidationIssue(new IValidationSupport.CodeValidationIssue(theMessage, this.myNonExistentCodeSystemSeverity, CodeValidationIssueCode.NOT_FOUND, CodeValidationIssueCoding.NOT_FOUND));
            }

            return result;
        }
    }

    @Nullable
    public IValidationSupport.CodeValidationResult validateCodeInValueSet(ValidationSupportContext theValidationSupportContext, ConceptValidationOptions theOptions, String theCodeSystem, String theCode, String theDisplay, @Nonnull IBaseResource theValueSet) {
        return !this.canValidateCodeSystem(theValidationSupportContext, theCodeSystem) ? null : (new IValidationSupport.CodeValidationResult()).setCode(theCode).setSeverity(IssueSeverity.INFORMATION).setMessage("Code " + theCodeSystem + "#" + theCode + " was not checked because the CodeSystem is not available");
    }

    private boolean allowNonExistentCodeSystems() {
        switch (this.myNonExistentCodeSystemSeverity) {
            case ERROR:
            case FATAL:
                return false;
            case WARNING:
            case INFORMATION:
                return true;
            default:
                ourLog.info("Unknown issue severity " + this.myNonExistentCodeSystemSeverity.name() + ". Treating as INFO/WARNING");
                return true;
        }
    }

    private boolean canValidateCodeSystem(ValidationSupportContext theValidationSupportContext, String theCodeSystem) {
        if (!this.allowNonExistentCodeSystems()) {
            return false;
        } else if (theCodeSystem == null) {
            return false;
        } else if (this.whiteListCodeSystemRegex.stream().anyMatch(theCodeSystem::matches)) {
            return false;
        } else {
            IBaseResource codeSystem = theValidationSupportContext.getRootValidationSupport().fetchCodeSystem(theCodeSystem);
            return codeSystem == null;
        }
    }

    /** @deprecated */
    @Deprecated
    public void setAllowNonExistentCodeSystem(boolean theAllowNonExistentCodeSystem) {
        if (theAllowNonExistentCodeSystem) {
            this.setNonExistentCodeSystemSeverity(IssueSeverity.WARNING);
        } else {
            this.setNonExistentCodeSystemSeverity(IssueSeverity.ERROR);
        }

    }

    public void setNonExistentCodeSystemSeverity(@Nonnull IValidationSupport.IssueSeverity theSeverity) {
        Validate.notNull(theSeverity, "theSeverity must not be null", new Object[0]);
        this.myNonExistentCodeSystemSeverity = theSeverity;
    }

    static {
        DEFAULT_SEVERITY = IssueSeverity.ERROR;
    }
}
