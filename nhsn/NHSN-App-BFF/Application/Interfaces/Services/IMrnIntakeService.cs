using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Reads and writes the MrnIntakeRecords row for the current facility (resolved internally via
// INhsnUserContext, same convention as IHslocMappingService) and reads the patient identifiers
// the rule builder works against. BFF-owned: there is no Link microservice concept of "which
// identifier is the usable MRN" to defer to.
public interface IMrnIntakeService
{
    // Null when the facility has never saved one yet.
    Task<MrnIntakeResponse?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MrnIntakeResponse intake, CancellationToken cancellationToken = default);

    // The patients the rule builder can review. Real patient ids, but see
    // IPatientIdentifierGateway's doc comment for why their identifier elements are fixture-backed
    // today.
    Task<IReadOnlyList<PatientIdentifierResponse>> GetPatientIdentifiersAsync(CancellationToken cancellationToken = default);

    // The checkbox option sets for the step's three "select all that apply" questions. Same for
    // every facility — seeded reference data, not per-facility state.
    Task<MrnIntakeOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken = default);
}
