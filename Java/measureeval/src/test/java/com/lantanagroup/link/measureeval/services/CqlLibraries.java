package com.lantanagroup.link.measureeval.services;

/**
 * Utility class containing predefined Clinical Quality Language (CQL) libraries as static constants.
 * These libraries are used for testing and evaluation of FHIR measures in various scenarios,
 * including cohort, proportion, ratio, and continuous variable measures.
 */
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
            library ProportionLibraryAllFalse version '1.0.0'
                            
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
              "Initial Population" IP where false
                            
            define function "Measure Observation"(Encounter "Encounter"):
              24""";

    /**
     * Rebuilds a MedicationRequest field by field down to Timing.Repeat, the shape the NHSN ACH 3.0.0 SDE uses.
     * Copying the list-valued enumeration elements (dayOfWeek, when) crashed clinical-reasoning before 4.9.0.
     */
    public static final String COHORT_WITH_REBUILT_MEDICATION_REQUEST_SDE = """
            library CohortLibraryWithRebuiltMedicationRequestSDE version '1.0.0'

            using FHIR version '4.0.1'

            context Patient

            define "Initial Population":
              ["Encounter"] E
                where E.status.value = 'finished'

            define "SDE Medication Request":
              ["MedicationRequest"] M
                where exists "Initial Population"
                return MedicationRequestResource(M)

            define function MedicationRequestRepeat(repeat FHIR.Timing.Repeat):
              repeat r
              return FHIR.Timing.Repeat{
                bounds: r.bounds,
                count: r.count,
                countMax: r.countMax,
                "duration": r."duration",
                durationMax: r.durationMax,
                durationUnit: r.durationUnit,
                frequency: r.frequency,
                frequencyMax: r.frequencyMax,
                period: r.period,
                periodMax: r.periodMax,
                periodUnit: r.periodUnit,
                dayOfWeek: r.dayOfWeek,
                timeOfDay: r.timeOfDay,
                "when": r."when",
                offset: r.offset
              }

            define function MedicationRequestTiming(timing FHIR.Timing):
              timing t
              return FHIR.Timing{
                event: t.event,
                repeat: MedicationRequestRepeat(t.repeat),
                code: t.code
              }

            define function MedicationRequestDosageInstruction(dosageInstruction List<FHIR.Dosage>):
              dosageInstruction dI
              return FHIR.Dosage{
                text: dI.text,
                timing: MedicationRequestTiming(dI.timing)
              }

            define function MedicationRequestResource(medicationRequest MedicationRequest):
              medicationRequest m
              return MedicationRequest{
                id: FHIR.id {value: 'TST-' + m.id.value},
                status: m.status,
                intent: m.intent,
                medication: m.medication,
                subject: m.subject,
                authoredOn: m.authoredOn,
                dosageInstruction: MedicationRequestDosageInstruction(m.dosageInstruction)
              }""";

}
