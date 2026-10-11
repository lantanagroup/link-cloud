using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Link.UI.Models;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// One patient's mapping and pipeline diagnostics on a report. The mapping badge comes from the
/// entry row the patient list already loaded; the diagnostics are one entry read, made only when
/// someone opens that patient.
/// </summary>
public sealed class ReportPatientDiagnostics
{
    private readonly IReportServiceClient? _reports;

    public ReportPatientDiagnostics(IReportServiceClient? reports) => _reports = reports;

    public static ReportPatientDiagnostics Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        return new ReportPatientDiagnostics(
            string.IsNullOrWhiteSpace(registry.ReportServiceUrl) ? null : services.GetRequiredService<IReportServiceClient>());
    }

    public async Task<ReportPatientDetail> LoadAsync(string? facilityId, string? reportId, string? patientId, CancellationToken cancellationToken)
    {
        var patient = patientId?.Trim() ?? string.Empty;
        var report = reportId?.Trim();
        if (patient.Length == 0 || string.IsNullOrWhiteSpace(report))
            return new ReportPatientDetail { PatientId = patient, Error = "Patient and report are required." };
        if (_reports is null)
            return new ReportPatientDetail { PatientId = patient, Error = "The Report service is not configured." };

        var response = await _reports.GetEntryByScheduleAndPatientAsync(report, patient, cancellationToken);
        if (response.StatusCode == StatusCodes.Status404NotFound || response.Body is null)
            return new ReportPatientDetail { PatientId = patient, Error = "That patient is not on this report." };

        if (!response.IsSuccessStatusCode)
        {
            return new ReportPatientDetail
            {
                PatientId = patient,
                Error = FacilityFormRules.ServiceMessage("Report", response.StatusCode, response.RawBody)
            };
        }

        var entry = response.Body;
        if (!string.IsNullOrWhiteSpace(facilityId)
            && !string.IsNullOrWhiteSpace(entry.FacilityId)
            && !string.Equals(entry.FacilityId, facilityId.Trim(), StringComparison.Ordinal))
            return new ReportPatientDetail { PatientId = patient, Error = "That patient is not on this report." };

        return ToDetail(entry);
    }

    public static ReportPatientDetail ToDetail(ReportEntryDetailApiModel entry)
    {
        var acquisition = entry.Acquisition?.LocationOrg;
        var maps = entry.Normalization?.CodeMaps ?? [];
        return new ReportPatientDetail
        {
            PatientId = entry.PatientId,
            AcquisitionMissing = entry.Acquisition is null,
            EncounterCount = acquisition?.EncounterCount ?? 0,
            OrgEncounterCount = acquisition?.OrgEncounterCount ?? 0,
            AssumedOrgEncounterCount = acquisition?.AssumedOrgEncounterCount ?? 0,
            Locations = acquisition?.Matches
                .Select(match => string.IsNullOrWhiteSpace(match.LocationName) ? match.LocationId : $"{match.LocationName} ({match.LocationId})")
                .ToList() ?? [],
            NormalizationMissing = entry.Normalization is null,
            CodeMaps = maps.Select(map =>
            {
                var unmapped = map.UnmappedCodes.Count == 0 ? "" : " Unmapped codes: " + string.Join(", ", map.UnmappedCodes) + ".";
                return $"{map.SourceSystem} to {map.TargetSystem}: {map.MappedCount} mapped, {map.UnmappedCount} unmapped, {map.FailureCount} failed.{unmapped}";
            }).ToList()
        };
    }

    /// <summary>The worst of the three mapping indicators decides the badge; each one stays readable.</summary>
    public static ManifestMapping Mapping(MappingIndicatorStatus locationOrg, MappingIndicatorStatus encounterMapping, MappingIndicatorStatus hsloc)
    {
        var all = new[] { locationOrg, encounterMapping, hsloc };
        var (label, badge) =
            all.Contains(MappingIndicatorStatus.Unmapped) ? ("Not mapped", "au-badge-danger")
            : all.Contains(MappingIndicatorStatus.Unknown) ? ("Unknown", "au-badge-danger")
            : all.Contains(MappingIndicatorStatus.PartiallyMapped) ? ("Partial", "au-badge-warning")
            : all.Contains(MappingIndicatorStatus.NotEvaluated) ? ("Not evaluated", "au-badge-muted")
            : all.Contains(MappingIndicatorStatus.Mapped) ? ("Mapped", "au-badge-success")
            : ("Not applicable", "au-badge-muted");
        return new ManifestMapping
        {
            Label = label,
            BadgeClass = badge,
            LocationOrg = Words(locationOrg),
            EncounterMapping = Words(encounterMapping),
            Hsloc = Words(hsloc)
        };
    }

    public static string Words(MappingIndicatorStatus status) => status switch
    {
        MappingIndicatorStatus.NotEvaluated => "Not evaluated",
        MappingIndicatorStatus.NotApplicable => "Not applicable",
        MappingIndicatorStatus.Mapped => "Mapped",
        MappingIndicatorStatus.PartiallyMapped => "Partially mapped",
        MappingIndicatorStatus.Unmapped => "Unmapped",
        MappingIndicatorStatus.Unknown => "Unknown",
        _ => status.ToString()
    };
}
