package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.*;

public class PatientDataBuilder {

    public static Patient patient() {
        var patient = new Patient();
        patient.setId("Patient/simple-patient");
        return patient;
    }

    public static Encounter encounter() {
        var encounter = new Encounter();
        encounter.setId("Encounter/simple-encounter");
        encounter.setSubject(new Reference("Patient/simple-patient"));
        encounter.setStatus(Encounter.EncounterStatus.FINISHED);
        encounter.setClass_(new Coding().setSystem("http://terminology.hl7.org/CodeSystem/v3-ActCode").setCode("AMB"));
        return encounter;
    }

    public static Bundle patientOnlyBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(patient());
        return bundle;
    }

    public static Bundle patientAndEncounterBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(patient());
        bundle.addEntry().setResource(encounter());
        return bundle;
    }
}
