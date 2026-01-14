package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.FhirVersionEnum;
import ca.uhn.fhir.context.support.*;
import ca.uhn.fhir.i18n.Msg;
import ca.uhn.fhir.rest.api.SummaryEnum;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.gclient.IOperationUnnamed;
import ca.uhn.fhir.rest.gclient.IQuery;
import ca.uhn.fhir.rest.server.exceptions.BaseServerResponseException;
import ca.uhn.fhir.rest.server.exceptions.InvalidRequestException;
import ca.uhn.fhir.rest.server.exceptions.ResourceNotFoundException;
import ca.uhn.fhir.util.BundleUtil;
import ca.uhn.fhir.util.ParametersUtil;
import jakarta.annotation.Nonnull;
import jakarta.annotation.Nullable;
import org.apache.commons.lang3.StringUtils;
import org.apache.commons.lang3.Validate;
import org.hl7.fhir.common.hapi.validation.support.BaseValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.RemoteTerminologyUtil;
import org.hl7.fhir.instance.model.api.IBaseBundle;
import org.hl7.fhir.instance.model.api.IBaseParameters;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.CodeSystem;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

public class RemoteTermServiceValidation extends BaseValidationSupport implements IValidationSupport {
    private static final Logger ourLog = LoggerFactory.getLogger(RemoteTermServiceValidation.class);
    private String myBaseUrl;
    private final List<Object> myClientInterceptors = new ArrayList();
    private List<String> whiteListCodeSystemRegex = new ArrayList<>();
    private List<String> whiteListValueSetRegex = new ArrayList<>();
    private ValidationCacheService validationCacheService;

    public RemoteTermServiceValidation(FhirContext theFhirContext) {
        super(theFhirContext);
    }

    public RemoteTermServiceValidation(ValidationCacheService validationCacheService, FhirContext theFhirContext, String theBaseUrl, List<String> whiteListCodeSystemRegex, List<String> whiteListValueSetRegex) {
        super(theFhirContext);
        this.validationCacheService = validationCacheService;
        this.myBaseUrl = theBaseUrl;
        this.whiteListCodeSystemRegex = whiteListCodeSystemRegex;
        this.whiteListValueSetRegex = whiteListValueSetRegex;
    }

    public String getName() {
        return this.getFhirContext().getVersion().getVersion() + " Remote Terminology Service Validation Support";
    }

    public IValidationSupport.CodeValidationResult validateCode(ValidationSupportContext theValidationSupportContext, ConceptValidationOptions theOptions, String theCodeSystem, String theCode, String theDisplay, String theValueSetUrl) {
        return validationCacheService.cachedValidateCode(this, theCodeSystem, theCode, theDisplay, theValueSetUrl);
    }

    @Nullable
    private IBaseResource fetchCodeSystem(String theSystem, @Nullable SummaryEnum theSummaryParam) {
        IGenericClient client = this.provideClient();
        Class<? extends IBaseBundle> bundleType = this.myCtx.getResourceDefinition("Bundle").getImplementingClass(IBaseBundle.class);
        IQuery<IBaseBundle> codeSystemQuery = client.search().forResource("CodeSystem").where(CodeSystem.URL.matches().value(theSystem));
        if (theSummaryParam != null) {
            codeSystemQuery.summaryMode(theSummaryParam);
        }

        IBaseBundle results = (IBaseBundle)codeSystemQuery.returnBundle(bundleType).execute();
        List<IBaseResource> resultsList = BundleUtil.toListOfResources(this.myCtx, results);
        return !resultsList.isEmpty() ? (IBaseResource)resultsList.get(0) : null;
    }

    public IValidationSupport.LookupCodeResult lookupCode(ValidationSupportContext theValidationSupportContext, @Nonnull LookupCodeRequest theLookupCodeRequest) {
        String code = theLookupCodeRequest.getCode();
        String system = theLookupCodeRequest.getSystem();
        String displayLanguage = theLookupCodeRequest.getDisplayLanguage();
        Validate.notBlank(code, "theCode must be provided", new Object[0]);
        IGenericClient client = this.provideClient();
        FhirContext fhirContext = client.getFhirContext();
        FhirVersionEnum fhirVersion = fhirContext.getVersion().getVersion();
        if (!fhirVersion.isNewerThan(FhirVersionEnum.R4) && !fhirVersion.isOlderThan(FhirVersionEnum.DSTU3)) {
            IBaseParameters params = ParametersUtil.newInstance(fhirContext);
            ParametersUtil.addParameterToParametersString(fhirContext, params, "code", code);
            if (!StringUtils.isEmpty(system)) {
                ParametersUtil.addParameterToParametersString(fhirContext, params, "system", system);
            }

            if (!StringUtils.isEmpty(displayLanguage)) {
                ParametersUtil.addParameterToParametersString(fhirContext, params, "language", displayLanguage);
            }

            for(String propertyName : theLookupCodeRequest.getPropertyNames()) {
                ParametersUtil.addParameterToParametersCode(fhirContext, params, "property", propertyName);
            }

            Class<? extends IBaseResource> codeSystemClass = this.myCtx.getResourceDefinition("CodeSystem").getImplementingClass();

            IBaseParameters outcome;
            try {
                outcome = (IBaseParameters)((IOperationUnnamed)client.operation().onType(codeSystemClass)).named("$lookup").withParameters(params).useHttpGet().execute();
            } catch (InvalidRequestException | ResourceNotFoundException e) {
                ourLog.error(((BaseServerResponseException)e).getMessage(), e);
                IValidationSupport.LookupCodeResult result = LookupCodeResult.notFound(system, code);
                result.setErrorMessage(this.getErrorMessage("unknownCodeInSystem", system, code, client.getServerBase(), ((BaseServerResponseException)e).getMessage()));
                return result;
            }

            if (outcome != null && !outcome.isEmpty()) {
                return this.generateLookupCodeResult(code, system, (org.hl7.fhir.r4.model.Parameters)outcome);
            }

            return LookupCodeResult.notFound(system, code);
        } else {
            String var10002 = Msg.code(710);
            throw new UnsupportedOperationException(var10002 + "Unsupported FHIR version '" + fhirVersion.getFhirVersionString() + "'. Only DSTU3 and R4 are supported.");
        }
    }

    protected String getErrorMessage(String errorCode, Object... theParams) {
        return this.getFhirContext().getLocalizer().getMessage(this.getClass(), errorCode, theParams);
    }

    private IValidationSupport.LookupCodeResult generateLookupCodeResult(String theCode, String theSystem, org.hl7.fhir.r4.model.Parameters outcomeR4) {
        IValidationSupport.LookupCodeResult result = new IValidationSupport.LookupCodeResult();
        result.setSearchedForCode(theCode);
        result.setSearchedForSystem(theSystem);
        result.setFound(true);

        for(org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent parameterComponent : outcomeR4.getParameter()) {
            String parameterTypeAsString = Objects.toString(parameterComponent.getValue(), (String)null);
            switch (parameterComponent.getName()) {
                case "property":
                    IValidationSupport.BaseConceptProperty conceptProperty = createConceptProperty(parameterComponent);
                    if (conceptProperty != null) {
                        result.getProperties().add(conceptProperty);
                    }
                    break;
                case "designation":
                    IValidationSupport.ConceptDesignation conceptDesignation = this.createConceptDesignation(parameterComponent);
                    result.getDesignations().add(conceptDesignation);
                    break;
                case "name":
                    result.setCodeSystemDisplayName(parameterTypeAsString);
                    break;
                case "version":
                    result.setCodeSystemVersion(parameterTypeAsString);
                    break;
                case "display":
                    result.setCodeDisplay(parameterTypeAsString);
                    break;
                case "abstract":
                    result.setCodeIsAbstract(Boolean.parseBoolean(parameterTypeAsString));
            }
        }

        return result;
    }

    private static IValidationSupport.BaseConceptProperty createConceptProperty(org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent thePropertyComponent) {
        org.hl7.fhir.r4.model.Property property = thePropertyComponent.getChildByName("part");
        if (property != null && property.getValues().size() >= 2) {
            List<org.hl7.fhir.r4.model.Base> values = property.getValues();
            org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent firstPart = (org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent)values.get(0);
            String propertyName = (String)((org.hl7.fhir.r4.model.CodeType)firstPart.getValue()).getValue();
            org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent secondPart = (org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent)values.get(1);
            org.hl7.fhir.r4.model.Type value = secondPart.getValue();
            if (value != null) {
                return createConceptProperty(propertyName, value);
            } else {
                String groupName = secondPart.getName();
                if (!"subproperty".equals(groupName)) {
                    return null;
                } else {
                    IValidationSupport.GroupConceptProperty groupConceptProperty = new IValidationSupport.GroupConceptProperty(propertyName);

                    for(int i = 1; i < values.size(); ++i) {
                        org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent nextPart = (org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent)values.get(i);
                        IValidationSupport.BaseConceptProperty subProperty = createConceptProperty(nextPart);
                        if (subProperty != null) {
                            groupConceptProperty.addSubProperty(subProperty);
                        }
                    }

                    return groupConceptProperty;
                }
            }
        } else {
            return null;
        }
    }

    private static IValidationSupport.BaseConceptProperty createConceptProperty(String theName, org.hl7.fhir.r4.model.Type theValue) {
        IValidationSupport.BaseConceptProperty conceptProperty;
        switch (theValue.fhirType()) {
            case "string":
                org.hl7.fhir.r4.model.StringType stringType = (org.hl7.fhir.r4.model.StringType)theValue;
                conceptProperty = new IValidationSupport.StringConceptProperty(theName, (String)stringType.getValue());
                break;
            case "Coding":
                org.hl7.fhir.r4.model.Coding coding = (org.hl7.fhir.r4.model.Coding)theValue;
                conceptProperty = new IValidationSupport.CodingConceptProperty(theName, coding.getSystem(), coding.getCode(), coding.getDisplay());
                break;
            default:
                conceptProperty = new IValidationSupport.StringConceptProperty(theName, theValue.toString());
        }

        return conceptProperty;
    }

    private IValidationSupport.ConceptDesignation createConceptDesignation(org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent theParameterComponent) {
        IValidationSupport.ConceptDesignation conceptDesignation = new IValidationSupport.ConceptDesignation();

        for(org.hl7.fhir.r4.model.Parameters.ParametersParameterComponent designationComponent : theParameterComponent.getPart()) {
            org.hl7.fhir.r4.model.Type designationComponentValue = designationComponent.getValue();
            if (designationComponentValue != null) {
                switch (designationComponent.getName()) {
                    case "language":
                        conceptDesignation.setLanguage(designationComponentValue.toString());
                        break;
                    case "use":
                        org.hl7.fhir.r4.model.Coding coding = (org.hl7.fhir.r4.model.Coding)designationComponentValue;
                        conceptDesignation.setUseSystem(coding.getSystem());
                        conceptDesignation.setUseCode(coding.getCode());
                        conceptDesignation.setUseDisplay(coding.getDisplay());
                        break;
                    case "value":
                        conceptDesignation.setValue(designationComponentValue.toString());
                }
            }
        }

        return conceptDesignation;
    }

    public IBaseResource fetchValueSet(String theValueSetUrl) {
        SummaryEnum summaryParam = SummaryEnum.FALSE;
        return this.fetchValueSet(theValueSetUrl, summaryParam);
    }

    @Nullable
    private IBaseResource fetchValueSet(String theValueSetUrl, SummaryEnum theSummaryParam) {
        IGenericClient client = this.provideClient();
        Class<? extends IBaseBundle> bundleType = this.myCtx.getResourceDefinition("Bundle").getImplementingClass(IBaseBundle.class);
        IQuery<IBaseBundle> valueSetQuery = client.search().forResource("ValueSet").where(CodeSystem.URL.matches().value(theValueSetUrl));
        if (theSummaryParam != null) {
            valueSetQuery.summaryMode(theSummaryParam);
        }

        IBaseBundle results = (IBaseBundle)valueSetQuery.returnBundle(bundleType).execute();
        List<IBaseResource> resultsList = BundleUtil.toListOfResources(this.myCtx, results);
        return !resultsList.isEmpty() ? (IBaseResource)resultsList.get(0) : null;
    }

    public boolean isCodeSystemSupported(ValidationSupportContext theValidationSupportContext, String theSystem) {
        if (theSystem == null) {
            return false;
        }

        for (String pattern : whiteListCodeSystemRegex) {
            if (theSystem.matches(pattern)) {
                return false;
            }
        }

        IBaseResource codeSystem = this.fetchCodeSystem(theSystem, SummaryEnum.TRUE);
        return codeSystem != null;
    }

    public boolean isValueSetSupported(ValidationSupportContext theValidationSupportContext, String theValueSetUrl) {
        if (theValueSetUrl == null) {
            return false;
        }

        for (String pattern : whiteListValueSetRegex) {
            if (theValueSetUrl.matches(pattern)) {
                return false;
            }
        }

        IBaseResource valueSet = this.fetchValueSet(theValueSetUrl, SummaryEnum.TRUE);
        return valueSet != null;
    }

    public TranslateConceptResults translateConcept(IValidationSupport.TranslateCodeRequest theRequest) {
        IGenericClient client = this.provideClient();
        FhirContext fhirContext = client.getFhirContext();
        IBaseParameters params = RemoteTerminologyUtil.buildTranslateInputParameters(fhirContext, theRequest);
        IBaseParameters outcome = (IBaseParameters)((IOperationUnnamed)client.operation().onType("ConceptMap")).named("$translate").withParameters(params).execute();
        return RemoteTerminologyUtil.translateOutcomeToResults(fhirContext, outcome);
    }

    private IGenericClient provideClient() {
        IGenericClient retVal = this.myCtx.newRestfulGenericClient(this.myBaseUrl);

        for(Object next : this.myClientInterceptors) {
            retVal.registerInterceptor(next);
        }

        return retVal;
    }

    public String getBaseUrl() {
        return this.myBaseUrl;
    }

    protected IValidationSupport.CodeValidationResult invokeRemoteValidateCode(String theCodeSystem, String theCode, String theDisplay, String theValueSetUrl, IBaseResource theValueSet) {
        if (StringUtils.isBlank(theCode)) {
            return null;
        } else {
            IGenericClient client = this.provideClient();
            IBaseParameters input = this.buildValidateCodeInputParameters(theCodeSystem, theCode, theDisplay, theValueSetUrl, theValueSet);
            String resourceType = "ValueSet";
            if (theValueSet == null && theValueSetUrl == null) {
                resourceType = "CodeSystem";
            }

            IBaseParameters output;
            try {
                output = (IBaseParameters)((IOperationUnnamed)client.operation().onType(resourceType)).named("validate-code").withParameters(input).execute();
            } catch (InvalidRequestException | ResourceNotFoundException ex) {
                ourLog.error(((BaseServerResponseException)ex).getMessage(), ex);
                IValidationSupport.CodeValidationResult result = new IValidationSupport.CodeValidationResult();
                result.setSeverity(IssueSeverity.ERROR);
                String errorMessage = this.buildErrorMessage(theCodeSystem, theCode, theValueSetUrl, theValueSet, client.getServerBase(), ((BaseServerResponseException)ex).getMessage());
                result.setMessage(errorMessage);
                return result;
            }

            List<String> resultValues = ParametersUtil.getNamedParameterValuesAsString(this.getFhirContext(), output, "result");
            if (!resultValues.isEmpty() && !StringUtils.isBlank((CharSequence)resultValues.get(0))) {
                Validate.isTrue(resultValues.size() == 1, "Response contained %d 'result' values", (long)resultValues.size());
                boolean success = "true".equalsIgnoreCase((String)resultValues.get(0));
                IValidationSupport.CodeValidationResult retVal = new IValidationSupport.CodeValidationResult();
                if (success) {
                    retVal.setCode(theCode);
                    List<String> displayValues = ParametersUtil.getNamedParameterValuesAsString(this.getFhirContext(), output, "display");
                    if (!displayValues.isEmpty()) {
                        retVal.setDisplay((String)displayValues.get(0));
                    }
                } else {
                    retVal.setSeverity(IssueSeverity.ERROR);
                    List<String> messageValues = ParametersUtil.getNamedParameterValuesAsString(this.getFhirContext(), output, "message");
                    if (!messageValues.isEmpty()) {
                        retVal.setMessage((String)messageValues.get(0));
                    }
                }

                return retVal;
            } else {
                return null;
            }
        }
    }

    private String buildErrorMessage(String theCodeSystem, String theCode, String theValueSetUrl, IBaseResource theValueSet, String theServerUrl, String theServerMessage) {
        return theValueSetUrl == null && theValueSet == null ? this.getErrorMessage("unknownCodeInSystem", theCodeSystem, theCode, theServerUrl, theServerMessage) : this.getErrorMessage("unknownCodeInValueSet", theCodeSystem, theCode, theValueSetUrl, theServerUrl, theServerMessage);
    }

    protected IBaseParameters buildValidateCodeInputParameters(String theCodeSystem, String theCode, String theDisplay, String theValueSetUrl, IBaseResource theValueSet) {
        IBaseParameters params = ParametersUtil.newInstance(this.getFhirContext());
        if (theValueSet == null && theValueSetUrl == null) {
            ParametersUtil.addParameterToParametersUri(this.getFhirContext(), params, "url", theCodeSystem);
            ParametersUtil.addParameterToParametersString(this.getFhirContext(), params, "code", theCode);
            if (StringUtils.isNotBlank(theDisplay)) {
                ParametersUtil.addParameterToParametersString(this.getFhirContext(), params, "display", theDisplay);
            }

            return params;
        } else {
            if (StringUtils.isNotBlank(theValueSetUrl)) {
                ParametersUtil.addParameterToParametersUri(this.getFhirContext(), params, "url", theValueSetUrl);
            }

            ParametersUtil.addParameterToParametersString(this.getFhirContext(), params, "code", theCode);
            if (StringUtils.isNotBlank(theCodeSystem)) {
                ParametersUtil.addParameterToParametersUri(this.getFhirContext(), params, "system", theCodeSystem);
            }

            if (StringUtils.isNotBlank(theDisplay)) {
                ParametersUtil.addParameterToParametersString(this.getFhirContext(), params, "display", theDisplay);
            }

            if (theValueSet != null) {
                ParametersUtil.addParameterToParameters(this.getFhirContext(), params, "valueSet", theValueSet);
            }

            return params;
        }
    }
}

