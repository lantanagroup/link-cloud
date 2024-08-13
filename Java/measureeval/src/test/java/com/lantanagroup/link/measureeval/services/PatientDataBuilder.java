package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.*;

import java.util.Date;

public class PatientDataBuilder {

    public static Patient simplePatient() {
        var patient = new Patient();
        patient.setId("Patient/simple-patient");
        return patient;
    }

    public static Patient patient(String id) {
        var patient = new Patient();
        patient.setId(id.startsWith("Patient/") ? id : "Patient/" + id);
        return patient;
    }

    public static Encounter simpleEncounter() {
        var encounter = new Encounter();
        encounter.setId("Encounter/simple-encounter");
        encounter.setSubject(new Reference("Patient/simple-patient"));
        encounter.setStatus(Encounter.EncounterStatus.FINISHED);
        encounter.setClass_(new Coding().setSystem("http://terminology.hl7.org/CodeSystem/v3-ActCode").setCode("AMB"));
        encounter.addType().addCoding().setCode("183452005").setSystem("http://snomed.info/sct");
        encounter.setPeriod(new Period().setStart(new DateTimeType("2024-01-10").getValue()).setEnd(new DateTimeType("2024-01-11").getValue()));
        return encounter;
    }

    public static Encounter encounter(
            String id, String subject, String status, String classSystem, String classCode, Date start, Date end) {
        var encounter = new Encounter();
        encounter.setId(id.startsWith("Encounter/") ? id : "Encounter/" + id);
        encounter.setSubject(new Reference(subject.startsWith("Patient/") ? subject : "Patient/" + subject));
        encounter.setStatus(Encounter.EncounterStatus.fromCode(status.toLowerCase()));
        encounter.setClass_(new Coding().setSystem(classSystem).setCode(classCode));
        encounter.setPeriod(new Period().setStart(start).setEnd(end));
        return encounter;
    }

    public static Condition simpleCondition() {
        var condition = new Condition();
        condition.setId("Condition/simple-condition");
        condition.setSubject(new Reference("Patient/simple-patient"));
        return condition;
    }

    public static Condition condition(String id, String subject, Date recordedDate) {
        var condition = new Condition();
        condition.setId(id.startsWith("Condition/") ? id : "Condition/" + id);
        condition.setSubject(new Reference(subject.startsWith("Patient/") ? subject : "Patient/" + subject));
        condition.setRecordedDate(recordedDate);
        return condition;
    }

    public static Bundle simplePatientOnlyBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(simplePatient());
        return bundle;
    }

    public static Bundle simplePatientAndEncounterBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(simplePatient());
        bundle.addEntry().setResource(simpleEncounter());
        return bundle;
    }

    public static Bundle simplePatientEncounterAndConditionBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(simplePatient());
        bundle.addEntry().setResource(simpleEncounter());
        bundle.addEntry().setResource(simpleCondition());
        return bundle;
    }
}
