import { Category } from "src/app/components/sub-pre-qual-report/sub-pre-qual-report-issues/sub-pre-qual-report-issues.component";

export const dummyCategories: Category[] = [
  {
    name: 'Minimum requirement not met for profile.',
    quantity: 3,
    guidance: 'Needs investigation, cardinality is not being met based on profile. Reference and review profile to meet profile requirements.',
    issues: [
      { name: 'STRUCTURE', message: 'MedicationRequest.requester: minimum required = 1, but only found 0 (from http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-medicationrequest|1.0.0-cibuild)', expression: 'Bundle.entry[1].resource.ofType(MedicationRequest).MedicationRequest', location: '1:567' },
      { name: 'STRUCTURE', message: 'Encounter.identifier: minimum required = 1, but only found 0 (from http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-encounter|1.0.0-cibuild)', expression: 'Bundle.entry[6].resource.ofType(Encounter).Encounter', location: '1:801' },
      { name: 'NULL', message: 'Encounter.identifier: minimum required = 1, but only found 0 (from http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-encounter|1.0.0-cibuild)', expression: 'Bundle.entry[4].resource.ofType(Encounter).Encounter', location: '1:801' },
    ]
  },
  {
    name: 'Does not match extensible ValueSet',
    quantity: 3,
    guidance: 'This could be indicative of a problem if the data element is part of the measure and would not enable the resource to be included in the measure calculation appropriately.',
    issues: [
      { name: 'CODEINVALID', message: 'None of the codings provided are in the value set \'US Core Condition Code\' (http://hl7.org/fhir/us/core/ValueSet/us-core-condition-code|3.1.1), and a coding should come from this value set unless it has no suitable code (note that the validator cannot judge what is suitable) (codes = http://snomed.info/sct#442311008)', expression: 'Bundle.entry[0].resource.ofType(Condition).code', location: '1:878' },
      { name: 'CODEINVALID', message: 'None of the codings provided are in the value set \'US Core Medication Codes (RxNorm)\' (http://hl7.org/fhir/us/core/ValueSet/us-core-medication-codes|3.1.1), and a coding should come from this value set unless it has no suitable code (note that the validator cannot judge what is suitable) (codes = http://www.nlm.nih.gov/research/umls/rxnorm#1161609)', expression: 'Bundle.entry[1].resource.ofType(MedicationRequest).medication.ofType(CodeableConcept)', location: '1:453' },
      { name: 'CODEINVALID', message: 'None of the codings provided are in the value set \'US Core Medication Codes (RxNorm)\' (http://hl7.org/fhir/us/core/ValueSet/us-core-medication-codes|3.1.1), and a coding should come from this value set unless it has no suitable code (note that the validator cannot judge what is suitable) (codes = http://www.nlm.nih.gov/research/umls/rxnorm#1161609)', expression: 'Bundle.entry[1].resource.ofType(MedicationRequest).medication.ofType(CodeableConcept)', location: '1:453' },
    ]
  },
  {
    name: 'Does not match preferred ValueSet',
    quantity: 4,
    guidance: 'This could be indicative of a problem if the data element is part of the measure and would not enable the resource to be included in the measure calculation appropriately.',
    issues: [
      { name: 'ABC', message: 'Lorem ipsum sit dolor amet.', expression: 'Lorem ipsum sit dolor amet.', location: '1:111' },
      { name: 'DEF', message: 'Consectetur adipiscing elit.', expression: 'Consectetur adipiscing elit.', location: '2:222' },
      { name: 'GHI', message: 'Aliquam egestas non urna eget maximus.', expression: 'Aliquam egestas non urna eget maximus.', location: '5:987' },
      { name: 'JKL', message: 'Cras ultricies fringilla arcu.', expression: 'Cras ultricies fringilla arcu.', location: '0:123' },
    ]
  },
  {
    name: 'Does not match a slice',
    quantity: 4,
    guidance: 'This could indicate an underlying issue in the resource (the resource is not validating). FHIR SME may need to review.',
    issues: [
      { name: 'LMNOP', message: 'Lorem ipsum sit dolor amet.', expression: 'Lorem ipsum sit dolor amet.', location: '1:111' },
      { name: 'QRS', message: 'Consectetur adipiscing elit.', expression: 'Consectetur adipiscing elit.', location: '2:222' },
      { name: 'TUV', message: 'Aliquam egestas non urna eget maximus.', expression: 'Aliquam egestas non urna eget maximus.', location: '5:987' },
      { name: 'WXYZ', message: 'Cras ultricies fringilla arcu.', expression: 'Cras ultricies fringilla arcu.', location: '0:123' },
    ]
  },
  {
    name: 'Identifier value starts with whitespace',
    quantity: 5,
    guidance: 'This is a business identifier with whitespace at the front or back. Not important if business identifiers are not used. May want to have the whitespace trimmed.',
    issues: [
      { name: 'TEST', message: 'Test test test.', expression: 'Test', location: '1:111' },
      { name: 'REST', message: 'Rest rest rest.', expression: 'Rest', location: '2:222' },
      { name: 'PEST', message: 'Pest pest pest.', expression: 'Pest', location: '3:333' },
      { name: 'QUEST', message: 'Quest quest quest.', expression: 'Quest', location: '4:444' },
      { name: 'STRUCTURE', message: 'Does not rhyme.', expression: 'Bundle.entry[0].resource.ofType(Condition).code', location: '5:555' },
    ]
  }
]