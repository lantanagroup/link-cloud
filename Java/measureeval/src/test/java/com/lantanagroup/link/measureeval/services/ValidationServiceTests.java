package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.r4.model.DomainResource;
import org.hl7.fhir.r4.model.Encounter;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

import java.util.List;

public class ValidationServiceTests {

    private final String ACH_DAILY_ENCOUNTER_PROFILE =
            "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-daily-encounter";
    private final String ACH_MONTHLY_ENCOUNTER_PROFILE =
            "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-monthly-encounter";

    private final FhirContext fhirContext = FhirContext.forR4();
    private static ValidationService validationService;

    @BeforeAll
    public static void setup() {
        validationService = new ValidationService(
                List.of("http://hl7.org/fhir/us/core/STU3.1.1/package.tgz"),
                List.of("/Users/christopherschuler/.fhir/packages/gov.cdc.nhsn.measures#dev"),
                true);
    }

    @Test
    void validateValidACHDailyEncounter() {
        var result = validationService.validate(
                getTestResource(ACH_DAILY_ENCOUNTER_EXAMPLE_VALID),
                List.of(ACH_DAILY_ENCOUNTER_PROFILE));
        // No validation messages
        Assertions.assertFalse(result.hasContained());
        // Tagged with profile
        Assertions.assertTrue(result.hasMeta());
        Assertions.assertTrue(result.getMeta().hasProfile(ACH_DAILY_ENCOUNTER_PROFILE));
    }

    @Test
    void validateInvalidACHDailyEncounter() {
        var invalidEncounter = (Encounter) getTestResource(ACH_DAILY_ENCOUNTER_EXAMPLE_VALID);
        invalidEncounter.setStatus(null);
        var result = validationService.validate(
                invalidEncounter,
                List.of(ACH_DAILY_ENCOUNTER_PROFILE));
        // Has validation messages
        Assertions.assertTrue(result.hasContained());
        // Has validation-result extension
        Assertions.assertTrue(result.hasExtension(ValidationService.VALIDATION_RESULT_EXT_URL));
        // Not tagged with profile
        Assertions.assertFalse(result.hasMeta());
        // Also validate truncation
        Assertions.assertFalse(((Encounter) result).hasPriority());
    }

    @Test
    void validateValidACHMonthlyEncounter() {
        var result = validationService.validate(
                getTestResource(ACH_MONTHLY_ENCOUNTER_EXAMPLE_VALID),
                List.of(ACH_MONTHLY_ENCOUNTER_PROFILE));
        // No validation messages
        Assertions.assertFalse(result.hasContained());
        // Tagged with profile
        Assertions.assertTrue(result.hasMeta());
        Assertions.assertTrue(result.getMeta().hasProfile(ACH_MONTHLY_ENCOUNTER_PROFILE));
    }

    @Test
    void validateInvalidACHMonthlyEncounter() {
        var invalidEncounter = (Encounter) getTestResource(ACH_MONTHLY_ENCOUNTER_EXAMPLE_VALID);
        invalidEncounter.setStatus(null);
        var result = validationService.validate(
                invalidEncounter,
                List.of(ACH_MONTHLY_ENCOUNTER_PROFILE));
        // Has validation messages
        Assertions.assertTrue(result.hasContained());
        // Has validation-result extension
        Assertions.assertTrue(result.hasExtension(ValidationService.VALIDATION_RESULT_EXT_URL));
        // Not tagged with profile
        Assertions.assertFalse(result.hasMeta());
    }

    @Test
    void validateACHDailyBundle() {
        // TODO: implement once the NHSN Measure IG model info is ready
    }

    @Test
    void validateACHMonthlyBundle() {
        // TODO: implement once the NHSN Measure IG model info is ready
    }

    private DomainResource getTestResource(String resourceJson) {
        return (DomainResource) fhirContext.newJsonParser().parseResource(resourceJson);
    }

    private final String ACH_DAILY_ENCOUNTER_EXAMPLE_VALID = """
            {
              "resourceType" : "Encounter",
              "id" : "encounter-example-ach-daily-influenzatherapeutic",
              "identifier" : [{
                "use" : "usual",
                "system" : "urn:oid:2.16.840.1.113883.19.5.1.698.8",
                "value" : "123456789987"
              }],
              "status" : "finished",
              "_status" : {
                "extension" : [{
                  "url" : "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-original-element-value-extension",
                  "valueString" : "TRIAGED"
                }]
              },
              "class" : {
                "system" : "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                "version" : "9.0.0",
                "code" : "ACUTE",
                "display" : "Inpatient Acute"
              },
              "type" : [{
                "coding" : [{
                  "system" : "http://snomed.info/sct",
                  "code" : "183452005",
                  "display" : "Emergency hospital admission (procedure)"
                }],
                "text" : "Emergency hospital admission"
              }],
              "priority": {
                "text": "This should be truncated!"
              },
              "subject" : {
                "reference" : "Patient/patient-example-ach-daily-influenzatherapeutic",
                "display" : "ACHDaily, InfluenzaTherapeutic"
              },
              "period" : {
                "start" : "2024-01-01T08:00:00Z",
                "end" : "2024-01-04T12:00:00Z"
              },
              "reasonCode" : [{
                "coding" : [{
                  "system" : "http://snomed.info/sct",
                  "code" : "274640006",
                  "display" : "Fever with chills"
                }],
                "text" : "Fever with chills"
              }],
              "hospitalization" : {
                "admitSource" : {
                  "coding" : [{
                    "system" : "http://terminology.hl7.org/CodeSystem/admit-source",
                    "code" : "gp",
                    "display" : "General Practitioner referral"
                  }],
                  "text" : "Direct admission from doctor's office (in same system)"
                },
                "dischargeDisposition" : {
                  "coding" : [{
                    "system" : "http://terminology.hl7.org/CodeSystem/discharge-disposition",
                    "code" : "home",
                    "display" : "Home"
                  }],
                  "text" : "Home"
                }
              },
              "location" : [{
                "location" : {
                  "reference" : "Location/location-example-ach-daily-emergency",
                  "display" : "EMERGENCY - PAVILION"
                },
                "period" : {
                  "start" : "2024-01-14T08:00:00Z",
                  "end" : "2024-01-22T12:00:00Z"
                }
              }]
            }""";

    private final String ACH_MONTHLY_ENCOUNTER_EXAMPLE_VALID = """
            {
              "resourceType" : "Encounter",
              "id" : "encounter-example-ach-monthly-pass1",
              "identifier" : [{
                "use" : "usual",
                "system" : "urn:oid:2.16.840.1.113883.19.5.1.698.8",
                "value" : "10005104251"
              }],
              "status" : "in-progress",
              "class" : {
                "system" : "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                "version" : "9.0.0",
                "code" : "ACUTE",
                "display" : "inpatient acute"
              },
              "type" : [{
                "coding" : [{
                  "system" : "http://snomed.info/sct",
                  "code" : "32485007",
                  "display" : "Hospital admission (procedure)"
                }],
                "text" : "Hospital Admission"
              }],
              "subject" : {
                "reference" : "Patient/patient-example-ach-monthly-pass1",
                "display" : "Pass1 ACH"
              },
              "period" : {
                "start" : "2024-02-01T16:02:00-05:00",
                "end" : "2024-02-04T19:00:00-05:00"
              },
              "reasonCode" : [{
                "coding" : [{
                  "system" : "http://hl7.org/fhir/sid/icd-10-cm",
                  "code" : "R50.9",
                  "display" : "Fever, unspecified"
                }],
                "text" : "Fever"
              }],
              "diagnosis" : [{
                "condition" : {
                  "reference" : "Condition/condition-example-diagnosis-ach-monthly-pass1",
                  "display" : "Thrombophlebitis"
                }
              }],
              "hospitalization" : {
                "admitSource" : {
                  "coding" : [{
                    "system" : "http://terminology.hl7.org/CodeSystem/admit-source",
                    "code" : "born",
                    "display" : "Born in hospital"
                  }],
                  "text" : "Born in hospital"
                },
                "dischargeDisposition" : {
                  "coding" : [{
                    "system" : "http://terminology.hl7.org/CodeSystem/discharge-disposition",
                    "code" : "oth",
                    "display" : "Other"
                  }],
                  "text" : "Other"
                }
              },
              "location" : [{
                "location" : {
                  "reference" : "Location/location-example-ach-monthly-nicu-level-iii",
                  "display" : "ACH Monthly Neonatal critical care"
                },
                "physicalType" : {
                  "coding" : [{
                    "system" : "http://terminology.hl7.org/CodeSystem/location-physical-type",
                    "code" : "wa",
                    "display" : "Ward"
                  }]
                },
                "period" : {
                  "start" : "2024-02-01T16:02:00-05:00",
                  "end" : "2024-02-02T16:02:00-05:00"
                }
              }]
            }""";
}
