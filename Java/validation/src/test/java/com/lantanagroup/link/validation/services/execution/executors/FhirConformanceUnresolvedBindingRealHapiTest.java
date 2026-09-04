package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.common.hapi.validation.support.CommonCodeSystemsTerminologyService;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.PrePopulatedValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.SnapshotGeneratingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Coding;
import org.hl7.fhir.r4.model.ElementDefinition;
import org.hl7.fhir.r4.model.Enumerations;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.StructureDefinition;
import org.hl7.fhir.r4.model.ValueSet;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * End-to-end guard against the REAL HAPI 8.10 InstanceValidator: a resource whose <em>only</em>
 * problems are unresolvable terminology bindings must produce zero ERROR-severity conformance findings
 * — every terminology signal is downgraded to "not evaluated". Unlike {@link FhirConformanceCheckExecutorTest}
 * (which stubs the validator with captured strings), this drives the actual validator, so it also fails
 * loudly if a future HAPI upgrade changes the message ids / phrasings the downgrade keys on.
 */
class FhirConformanceUnresolvedBindingRealHapiTest {

    private static final FhirContext CTX = FhirContext.forR4();
    private static final String PROFILE_URL = "http://example.org/StructureDefinition/unresolvable-obs";
    private static final String VS_URL = "http://example.org/ValueSet/does-not-exist";

    private FhirConformanceCheckExecutor executorWith(PrePopulatedValidationSupport pre) {
        ValidationSupportChain chain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(CTX), pre,
                new SnapshotGeneratingValidationSupport(CTX),
                new CommonCodeSystemsTerminologyService(CTX),
                new InMemoryTerminologyServerValidationSupport(CTX));
        FhirInstanceValidator module = new FhirInstanceValidator(chain);
        module.setAnyExtensionsAllowed(true);
        FhirValidator validator = CTX.newValidator();
        validator.registerValidatorModule(module);
        return new FhirConformanceCheckExecutor(validator, new ObjectMapper(), 10);
    }

    private StructureDefinition profileBoundToUnresolvableVs() {
        StructureDefinition sd = new StructureDefinition();
        sd.setUrl(PROFILE_URL).setName("UnresolvableObs").setStatus(Enumerations.PublicationStatus.ACTIVE);
        sd.setFhirVersion(Enumerations.FHIRVersion._4_0_1);
        sd.setKind(StructureDefinition.StructureDefinitionKind.RESOURCE).setAbstract(false).setType("Observation");
        sd.setBaseDefinition("http://hl7.org/fhir/StructureDefinition/Observation");
        sd.setDerivation(StructureDefinition.TypeDerivationRule.CONSTRAINT);
        ElementDefinition ed = sd.getDifferential().addElement();
        ed.setPath("Observation.code");
        ed.getBinding().setStrength(Enumerations.BindingStrength.REQUIRED).setValueSet(VS_URL);
        return sd;
    }

    private RubricCheck check() {
        return RubricCheck.builder()
                .checkLocalId("fc-real")
                .dimension(PiqiDimension.CONFORMANCE)
                .parametersJson("{\"profiles\":[\"" + PROFILE_URL + "\"]}")
                .build();
    }

    private ExecutionContext contextWith(String system, String code) {
        Observation obs = new Observation();
        obs.getMeta().addProfile(PROFILE_URL);
        obs.setStatus(Observation.ObservationStatus.FINAL);
        obs.setSubject(new Reference("Patient/1"));
        obs.setCode(new CodeableConcept().addCoding(new Coding().setSystem(system).setCode(code)));
        return ExecutionContext.builder().resource(obs).build();
    }

    @Test
    @DisplayName("value set absent from the chain: no ERROR survives; terminology is not-evaluated")
    void valueSetAbsentProducesNoErrors() {
        PrePopulatedValidationSupport pre = new PrePopulatedValidationSupport(CTX);
        pre.addStructureDefinition(profileBoundToUnresolvableVs());

        List<RawFinding> findings = executorWith(pre).execute(check(), contextWith("http://loinc.org", "1234-5"));

        assertNoConformanceErrors(findings);
        assertThat(findings).anySatisfy(f -> assertThat(f.isNotEvaluated()).isTrue());
    }

    @Test
    @DisplayName("value set present but its code system is unloaded: the membership ERROR is downgraded too")
    void valueSetPresentButCodeSystemUnloadedProducesNoErrors() {
        PrePopulatedValidationSupport pre = new PrePopulatedValidationSupport(CTX);
        pre.addStructureDefinition(profileBoundToUnresolvableVs());
        ValueSet vs = new ValueSet();
        vs.setUrl(VS_URL).setStatus(Enumerations.PublicationStatus.ACTIVE);
        vs.getCompose().addInclude().setSystem("http://example.org/CodeSystem/unknown-cs");
        pre.addValueSet(vs);

        List<RawFinding> findings = executorWith(pre)
                .execute(check(), contextWith("http://example.org/CodeSystem/unknown-cs", "abc"));

        // This is the case that previously leaked ERRORs ("None of the codings ... required").
        assertNoConformanceErrors(findings);
        assertThat(findings).anySatisfy(f -> assertThat(f.isNotEvaluated()).isTrue());
    }

    /**
     * The resource is structurally valid, so the only ERROR-capable signals are terminology ones — all
     * of which must now be "not evaluated". Any surviving code=="fhir-conformance" ERROR means the
     * downgrade missed a real HAPI phrasing/id.
     */
    private static void assertNoConformanceErrors(List<RawFinding> findings) {
        assertThat(findings)
                .filteredOn(f -> "fhir-conformance".equals(f.getCode()))
                .allSatisfy(f -> assertThat(f.getSeverity()).isNotEqualTo(Severity.ERROR));
    }
}
