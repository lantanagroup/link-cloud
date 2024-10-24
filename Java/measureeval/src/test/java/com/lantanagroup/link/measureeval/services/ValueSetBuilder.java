package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.Enumerations;
import org.hl7.fhir.r4.model.ValueSet;

public class ValueSetBuilder {

    public static ValueSet inpatientEncounter() {
        var system = "http://snomed.info/sct";
        var version = "http://snomed.info/sct/731000124108/version/20210901";
        var vs = new ValueSet();
        vs.setId("2.16.840.1.113883.3.666.5.307");
        vs.setUrl("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307");
        vs.setVersion("20200307");
        vs.setName("Encounter_Inpatient");
        vs.setStatus(Enumerations.PublicationStatus.ACTIVE);
        var expansion = new ValueSet.ValueSetExpansionComponent();
        expansion.addContains().setSystem(system).setVersion(version).setCode("183452005").setDisplay("Emergency hospital admission (procedure)");
        expansion.addContains().setSystem(system).setVersion(version).setCode("32485007").setDisplay("Hospital admission (procedure)");
        expansion.addContains().setSystem(system).setVersion(version).setCode("8715000").setDisplay("Hospital admission, elective (procedure)");
        vs.setExpansion(expansion);
        return vs;
    }
}
