// Cohort infrastructure lives in the Automation library (Generation namespace).
// This file re-exports the type so existing Automation.UI code continues to compile
// without changing every file that references PatientCohortDefinition.
global using PatientCohortDefinition = LantanaGroup.Automation.Generation.PatientCohortDefinition;
