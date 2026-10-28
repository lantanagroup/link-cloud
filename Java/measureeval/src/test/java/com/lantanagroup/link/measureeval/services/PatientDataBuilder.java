package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.*;

import java.util.Date;

/**
 * Utility class for building FHIR R4 resources such as {@link Patient}, {@link Encounter}, {@link Condition},
 * and {@link Bundle}. This class provides methods to create simple test resources and customized resources
 * for use in measure evaluation and testing.
 */
public class PatientDataBuilder {

    /**
     * Creates a simple {@link Patient} resource with a predefined ID.
     *
     * @return A {@link Patient} resource with ID "Patient/simple-patient".
     */
    public static Patient simplePatient() {
        var patient = new Patient();
        patient.setId("Patient/simple-patient");
        return patient;
    }

    /**
     * Creates a {@link Patient} resource with a specified ID.
     *
     * @param id The ID of the patient. If it does not start with "Patient/", it will be prefixed automatically.
     * @return A {@link Patient} resource with the specified ID.
     */
    public static Patient patient(String id) {
        var patient = new Patient();
        patient.setId(id.startsWith("Patient/") ? id : "Patient/" + id);
        return patient;
    }

    /**
     * Creates a simple {@link Encounter} resource with predefined attributes.
     *
     * @return An {@link Encounter} resource with a fixed ID, subject reference, status, class, type, and period.
     */
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

    /**
     * Creates a customized {@link Encounter} resource with specified attributes.
     *
     * @param id         The ID of the encounter. If it does not start with "Encounter/", it will be prefixed automatically.
     * @param subject    The reference to the patient. If it does not start with "Patient/", it will be prefixed automatically.
     * @param status     The status of the encounter (e.g., "finished", "in-progress").
     * @param classSystem The system URI for the encounter class.
     * @param classCode  The code for the encounter class.
     * @param start      The start date of the encounter.
     * @param end        The end date of the encounter.
     * @return A customized {@link Encounter} resource.
     */
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

    /**
     * Creates a simple {@link Condition} resource with predefined attributes.
     *
     * @return A {@link Condition} resource with a fixed ID and subject reference.
     */
    public static Condition simpleCondition() {
        var condition = new Condition();
        condition.setId("Condition/simple-condition");
        condition.setSubject(new Reference("Patient/simple-patient"));
        return condition;
    }

    /**
     * Creates a customized {@link Condition} resource with specified attributes.
     *
     * @param id           The ID of the condition. If it does not start with "Condition/", it will be prefixed automatically.
     * @param subject      The reference to the patient. If it does not start with "Patient/", it will be prefixed automatically.
     * @param recordedDate The recorded date of the condition.
     * @return A customized {@link Condition} resource.
     */
    public static Condition condition(String id, String subject, Date recordedDate) {
        var condition = new Condition();
        condition.setId(id.startsWith("Condition/") ? id : "Condition/" + id);
        condition.setSubject(new Reference(subject.startsWith("Patient/") ? subject : "Patient/" + subject));
        condition.setRecordedDate(recordedDate);
        return condition;
    }

    /**
     * Creates a simple {@link Bundle} resource containing only a single {@link Patient}.
     *
     * @return A {@link Bundle} resource with a single {@link Patient} entry.
     */
    public static Bundle simplePatientOnlyBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(simplePatient());
        return bundle;
    }

    /**
     * Creates a {@link Bundle} resource containing a {@link Patient} and an {@link Encounter}.
     *
     * @return A {@link Bundle} resource with {@link Patient} and {@link Encounter} entries.
     */
    public static Bundle simplePatientAndEncounterBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(simplePatient());
        bundle.addEntry().setResource(simpleEncounter());
        return bundle;
    }

    /**
     * Creates a {@link Bundle} resource containing a {@link Patient}, an {@link Encounter}, and a {@link Condition}.
     *
     * @return A {@link Bundle} resource with {@link Patient}, {@link Encounter}, and {@link Condition} entries.
     */
    public static Bundle simplePatientEncounterAndConditionBundle() {
        var bundle = new Bundle();
        bundle.addEntry().setResource(simplePatient());
        bundle.addEntry().setResource(simpleEncounter());
        bundle.addEntry().setResource(simpleCondition());
        return bundle;
    }
}
