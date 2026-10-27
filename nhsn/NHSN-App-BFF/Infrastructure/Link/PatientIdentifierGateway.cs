using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// Patient ids are real, sourced from IReportGateway.GetFacilityPatientIdsAsync (the facility's
// generated-report patients). Only the Patient.identifier elements below are fixture data — see
// IPatientIdentifierGateway's doc comment for why. Two identifiers per patient, an enterprise MPI
// id (fully populated) and a legacy MRN (missing use/assigner/period), so the UI's "N/A" rendering
// and the rule builder are both exercisable against a live BFF rather than only the frontend's own
// MockApiClient.
internal sealed class PatientIdentifierGateway : IPatientIdentifierGateway
{
    private readonly IReportGateway _reportGateway;

    public PatientIdentifierGateway(IReportGateway reportGateway)
    {
        _reportGateway = reportGateway;
    }

    public async Task<IReadOnlyList<PatientIdentifierResponse>> GetForFacilityAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var patientIds = await _reportGateway.GetFacilityPatientIdsAsync(facilityId, cancellationToken);

        return patientIds
            .Select((patientId, index) => BuildPatientWithMockIdentifiers(patientId, index + 1))
            .ToArray();
    }

    private static PatientIdentifierResponse BuildPatientWithMockIdentifiers(string patientId, int ordinal)
    {
        return new PatientIdentifierResponse
        {
            PatientId = patientId,
            Elements =
            [
                new PatientIdentifierElementResponse
                {
                    Value = $"MPI-{1000 + ordinal}",
                    Type = "MR (Medical record number)",
                    System = "http://example.invalid/fhir/identifier/empi",
                    Use = "usual",
                    Assigner = "Simulated Facility",
                    PeriodStart = "2019-01-01"
                },
                new PatientIdentifierElementResponse
                {
                    Value = $"SIM-{patientId}",
                    Type = "MR (Medical record number)",
                    System = "http://example.invalid/fhir/identifier/legacy-mrn"
                }
            ]
        };
    }
}
