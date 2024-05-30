using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Application.ResourceCategories
{
    public static class ResourceCategory
    {
        private static List<string> PatientRelatedResources()
        {
            return new List<string>()
            {
                nameof(Account),
                nameof(AdverseEvent),
                nameof(AllergyIntolerance),
                nameof(Appointment),
                nameof(AppointmentResponse),
                nameof(AuditEvent),
                nameof(Basic),
                nameof(BodyStructure),
                nameof(CarePlan),
                nameof(CareTeam),
                nameof(ChargeItem),
                nameof(Claim),
                nameof(ClaimResponse),
                nameof(ClinicalImpression),
                nameof(Communication),
                nameof(CommunicationRequest),
                nameof(Composition),
                nameof(Condition),
                nameof(Consent),
                nameof(Coverage),
                nameof(CoverageEligibilityRequest),
                nameof(CoverageEligibilityResponse),
                nameof(DetectedIssue),
                nameof(DeviceRequest),
                nameof(DeviceUseStatement),
                nameof(DiagnosticReport),
                nameof(DocumentManifest),
                nameof(DocumentReference),
                nameof(Encounter),
                nameof(EnrollmentRequest),
                nameof(EpisodeOfCare),
                nameof(ExplanationOfBenefit),
                nameof(FamilyMemberHistory),
                nameof(Flag),
                nameof(Goal),
                //TODO: Daniel - Should this be categorized as patient related?
                nameof(Group),
                nameof(ImagingStudy),
                nameof(Immunization),
                nameof(ImmunizationEvaluation),
                nameof(ImmunizationRecommendation),
                nameof(Invoice),
                //TODO: Daniel - Should this be categorized as patient related?
                nameof(List),
                nameof(MeasureReport),
                nameof(Media),
                nameof(MedicationAdministration),
                nameof(MedicationDispense),
                nameof(MedicationRequest),
                nameof(MedicationStatement),
                nameof(MolecularSequence),
                nameof(NutritionOrder),
                nameof(Observation),
                nameof(Patient),
                nameof(Person),
                nameof(Procedure),
                nameof(Provenance),
                nameof(QuestionnaireResponse),
                nameof(RelatedPerson),
                nameof(RequestGroup),
                nameof(ResearchSubject),
                nameof(RiskAssessment),
                nameof(Schedule),
                nameof(ServiceRequest),
                nameof(Specimen),
                nameof(SupplyDelivery),
                nameof(SupplyRequest),
                nameof(VisionPrescription)
            };
        }

        public static ResourceCategoryType? GetResourceCategoryByType(string typeName)
        {
            //Return null if the incoming type is not FHIR related
            if (!Enum.GetNames(typeof(FHIRDefinedType)).OfType<string>().ToList().Any(x => x == typeName)) 
            {
                return null;
            }

            if (PatientRelatedResources().Any(x => x == typeName))
            { 
                return ResourceCategoryType.Patient;
            }

            //TODO: Daniel - Potentially dangerous if we didn't add a patient resource to the PatientResourceTypes list.
            return ResourceCategoryType.Shared;
        }
    }
}

