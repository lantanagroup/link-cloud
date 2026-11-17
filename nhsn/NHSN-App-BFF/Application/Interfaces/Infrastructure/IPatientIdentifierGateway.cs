using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// A facility's patients and their FHIR Patient.identifier arrays, for the MRN Identifier Intake
// rule builder.
//
// Patient ids are real: they come from IReportGateway.GetFacilityPatientIdsAsync, the union of
// patient ids across the facility's generated reports (mirrors the onboarding POC's
// getMrnIntakePatients()). Only the Patient.identifier elements are fixture data -- no LinkSdk
// client exposes a report patient's real identifier array today. Census's own PatientIdentifier
// entity (DotNet/Census/Domain/Entities/POI) is Census-internal and isn't surfaced by
// IDataAcquisitionServiceClient, ICensusServiceClient or IReportServiceClient, and Report's own
// patient-export path explicitly excludes resource bodies (see
// IReportGateway.GetPatientMeasureReportExportAsync's remarks). See
// Settings/LinkCapabilitiesSettings.cs's PatientIdentifierLookup flag.
public interface IPatientIdentifierGateway
{
    Task<IReadOnlyList<PatientIdentifierResponse>> GetForFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
}
