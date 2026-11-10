using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;

/// <summary>
/// The outcome of a Patient search by MRN.
/// </summary>
/// <param name="MatchCount">Number of patients matched. Reported as 2 for "more than one", which a next page also implies.</param>
/// <param name="PatientId">The Patient.Id, when exactly one patient matched.</param>
public sealed record AdtPatientSearchResult(int MatchCount, string? PatientId);

/// <summary>
/// Finds the FHIR Patient.Id for an MRN on the facility's FHIR server.
/// </summary>
public interface IAdtPatientIdResolver
{
    /// <summary>
    /// Searches with <c>GET /Patient?identifier=&lt;system&gt;|&lt;mrn&gt;</c>. Failures reaching the server are thrown,
    /// not reported as no match, so that the SFTP acquisition is retried instead of events being dropped.
    /// </summary>
    Task<AdtPatientSearchResult> SearchByMrnAsync(
        string facilityId,
        string identifierSystem,
        string mrn,
        FhirQueryConfigurationModel queryConfiguration,
        CancellationToken cancellationToken);
}

/// <inheritdoc/>
public class AdtPatientIdResolver(ISearchFhirCommand searchFhirCommand) : IAdtPatientIdResolver
{
    /// <inheritdoc/>
    public async Task<AdtPatientSearchResult> SearchByMrnAsync(
        string facilityId,
        string identifierSystem,
        string mrn,
        FhirQueryConfigurationModel queryConfiguration,
        CancellationToken cancellationToken)
    {
        var searchParams = new SearchParams().Add("identifier", $"{identifierSystem}|{EscapeTokenValue(mrn)}");

        var bundle = await searchFhirCommand.ExecuteNonPagingAsync(
            new SearchFhirCommandRequest(
                queryConfiguration,
                ResourceType.Patient,
                searchParams,
                facilityId,
                patientId: null,
                correlationId: null,
                queryPhase: null,
                FhirQueryType.Search,
                reportTrackingId: null),
            cancellationToken);

        var patients = bundle.Entry.Select(e => e.Resource).OfType<Patient>().ToList();

        if (patients.Count > 1 || (patients.Count == 1 && bundle.Link.Exists(l => l.Relation == "next")))
        {
            return new AdtPatientSearchResult(2, null);
        }

        return patients.Count == 1
            ? new AdtPatientSearchResult(1, patients[0].Id)
            : new AdtPatientSearchResult(0, null);
    }

    /// <summary>
    /// Escapes the characters FHIR search treats as separators, so an MRN containing one is searched for literally.
    /// </summary>
    private static string EscapeTokenValue(string value) => value
        .Replace(@"\", @"\\")
        .Replace("|", @"\|")
        .Replace(",", @"\,")
        .Replace("$", @"\$");
}
