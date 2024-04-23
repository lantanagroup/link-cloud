package com.lantanagroup.link.shared.fhir;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.module.SimpleModule;
import org.hl7.fhir.r4.model.*;

import java.util.List;

public class FhirObjectMapper extends ObjectMapper {
    public FhirObjectMapper() {
        SimpleModule module = new SimpleModule();

        List<Class> classes = List.of(
                Resource.class,
                DomainResource.class,
                Appointment.class,
                Bundle.class,
                CapabilityStatement.class,
                CarePlan.class,
                CareTeam.class,
                Condition.class,
                Consent.class,
                Device.class,
                DiagnosticReport.class,
                DocumentReference.class,
                Encounter.class,
                ExplanationOfBenefit.class,
                Goal.class,
                HealthcareService.class,
                Immunization.class,
                Location.class,
                Medication.class,
                MedicationAdministration.class,
                MedicationDispense.class,
                MedicationRequest.class,
                MedicationStatement.class,
                Observation.class,
                Organization.class,
                Patient.class,
                Practitioner.class,
                Procedure.class,
                QuestionnaireResponse.class,
                RiskAssessment.class,
                Schedule.class,
                ServiceRequest.class,
                Task.class
        );

        for (Class clazz : classes) {
            module.addSerializer(new SpringSerializer<>(clazz));
            module.addDeserializer(clazz, new SpringDeserializer<>(clazz));
        }

        this.registerModule(module);
    }
}
