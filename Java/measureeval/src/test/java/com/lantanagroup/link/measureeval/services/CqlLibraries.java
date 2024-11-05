package com.lantanagroup.link.measureeval.services;

public class CqlLibraries {

    public static final String SIMPLE_COHORT_IP_TRUE = """
            library CohortLibraryTrue version '1.0.0'
                            
            using FHIR version '4.0.1'
                            
            context Patient

            define "Initial Population":
              true""";

    public static final String SIMPLE_COHORT_IP_FALSE = """
            library CohortLibraryFalse version '1.0.0'
                            
            using FHIR version '4.0.1'
                            
            context Patient

            define "Initial Population":
              false""";

    public static final String COHORT_IP_TRUE_WITH_VALUESET = """
            library CohortLibraryWithValueSetTrue version '1.0.0'
                            
            using FHIR version '4.0.1'
            
            valueset "Inpatient Encounter Codes": 'http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307'
            
            parameter "Measurement Period" default Interval[@2022-01-01, @2023-01-01)
                            
            context Patient

            define "Initial Population":
              "Inpatient Encounters"
              
            define "Inpatient Encounters":
              ["Encounter": type in "Inpatient Encounter Codes"] InpatientEncounter
                where InpatientEncounter.period.start.value in day of "Measurement Period"\s""";

    public static final String COHORT_IP_FALSE_WITH_VALUESET = """
            library CohortLibraryWithValueSetFalse version '1.0.0'
                            
            using FHIR version '4.0.1'
            
            valueset "Inpatient Encounter Codes": 'http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307'
            
            parameter "Measurement Period" default Interval[@2022-01-01, @2023-01-01)
                            
            context Patient

            define "Initial Population":
              "Inpatient Encounters"
              
            define "Inpatient Encounters":
              ["Encounter": type in "Inpatient Encounter Codes"] InpatientEncounter
                where not (InpatientEncounter.period.start.value in day of "Measurement Period")""";

    public static final String COHORT_IP_TRUE_WITH_SDE = """
            library CohortLibraryWithSDE version '1.0.0'
                            
            using FHIR version '4.0.1'
            
            valueset "Inpatient Encounter Codes": 'http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307'
            
            parameter "Measurement Period" default Interval[@2022-01-01, @2023-01-01)
                            
            context Patient

            define "Initial Population":
              "Inpatient Encounters"
              
            define "Inpatient Encounters":
              ["Encounter": type in "Inpatient Encounter Codes"] InpatientEncounter
                where InpatientEncounter.period.start.value in day of "Measurement Period"
            
            define "SDE Condition":
              ["Condition"] Conditions
                where exists(
                  "Initial Population"
                ) return ConditionResource(Conditions)
                
            define function ConditionResource(condition Condition):
              condition c
              return Condition{
                id: FHIR.id {value: 'TST-' + c.id.value},
                clinicalStatus: c.clinicalStatus,
                verificationStatus: c.verificationStatus,
                category: c.category,
                severity: c.severity,
                code: c.code,
                bodySite: c.bodySite,
                subject: c.subject,
                encounter: c.encounter,
                onset: c.onset,
                abatement: c.abatement,
                recordedDate: c.recordedDate,
                note: c.note
              }""";

    public static final String SIMPLE_PROPORTION_ALL_TRUE_NO_EXCLUSION = """
            library ProportionLibraryAllTrueNoExclusion version '1.0.0'
                            
            using FHIR version '4.0.1'
                            
            context Patient

            define "Initial Population":
              true
                                
            define "Numerator":
              true
              
            define "Numerator Exclusion":
              false
                            
            define "Denominator":
              true
              
            define "Denominator Exclusion":
              false""";

    public static final String SIMPLE_PROPORTION_ALL_FALSE = """
            library ProportionLibraryFalse version '1.0.0'
                            
            using FHIR version '4.0.1'
                            
            context Patient

            define "Initial Population":
              false
              
            define "Numerator":
              false
              
            define "Numerator Exclusion":
              false
                            
            define "Denominator":
              false
              
            define "Denominator Exclusion":
              false""";

    public static final String SIMPLE_RATIO = """
            library RatioLibrary version '1.0.0'
                            
            using FHIR version '4.0.1'
                            
            context Patient

            define "Initial Population":
              ["Encounter"]
              
            define "Numerator":
              "Initial Population"
                            
            define "Denominator":
              "Initial Population"
                            
            define function "Denominator Observation"(Encounter "Encounter"):
              24
              
            define function "Numerator Observation"(Encounter "Encounter"):
              1""";

    public static final String SIMPLE_CONTINUOUS_VARIABLE = """
            library ContinuousVariableLibrary version '1.0.0'
                            
            using FHIR version '4.0.1'
                            
            context Patient

            define "Initial Population":
              ["Encounter"]
              
            define "Measure Population":
              "Initial Population"
                            
            define "Measure Population Exclusion":
              false
                            
            define function "Measure Observation"(Encounter "Encounter"):
              24""";

}
