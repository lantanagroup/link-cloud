using System.Security.Cryptography;
using System.Text;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;

namespace Link.UI.Services;

/// <summary>
/// Saves report manifests in Automation's generation-manifest store, keyed by report id.
/// The key is a name-based id derived from the report id, so a lookup is one keyed read
/// however many reports exist. The Automation run that made a report keeps its own
/// run-keyed generation manifest; the saved section carries the run id back to it.
/// </summary>
public sealed class ReportManifestArchive
{
    public const string Domain = "generationManifest";
    private static readonly Guid Namespace = Guid.Parse("6f1c2a8e-3b4d-5e6f-8a9b-0c1d2e3f4a5b");
    private readonly ISnapshotStore _store;

    public ReportManifestArchive(ISnapshotStore store) => _store = store;

    public static ReportManifestArchive? From(IServiceProvider services)
    {
        try
        {
            var store = services.GetService<ISnapshotStore>();
            return store is null ? null : new ReportManifestArchive(store);
        }
        catch (Exception)
        {
            // Automation storage is optional; without it the manifest is read live.
            return null;
        }
    }

    /// <summary>RFC 4122 version 5 id for a report, so the same report always maps to the same key.</summary>
    public static Guid KeyFor(Guid reportId)
    {
        var name = Encoding.UTF8.GetBytes("report-manifest:" + reportId.ToString("D"));
        var space = Namespace.ToByteArray();
        SwapOrder(space);
        var hash = SHA1.HashData(space.Concat(name).ToArray());
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        SwapOrder(bytes);
        return new Guid(bytes);
    }

    public async Task<ReportManifestSection?> ReadAsync(Guid reportId, CancellationToken cancellationToken)
        => (await _store.GetDomainAsync<GenerationManifestSnapshot>(KeyFor(reportId), Domain, cancellationToken))?.Data?.Report;

    public Task SaveAsync(ReportManifestSection section, CancellationToken cancellationToken)
    {
        var reportId = Guid.Parse(section.ReportId);
        return _store.SetDomainAsync(
            KeyFor(reportId),
            Domain,
            new GenerationManifestSnapshot
            {
                PatientCount = section.PatientCount,
                TotalResourceCount = section.ResourceTypeCounts.Values.Sum(),
                SelectedMeasures = section.Measures,
                TotalCountsByType = section.ResourceTypeCounts,
                Report = section
            },
            cancellationToken);
    }

    private static void SwapOrder(byte[] guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
/// <summary>Lets the run engine save a report manifest through <see cref="ReportsService"/>.</summary>
public sealed class ReportManifestSaver(ReportsService reports) : Automation.UI.Services.IReportManifestSaver
{
    public Task<bool> SaveAsync(string facilityId, string reportId, Guid? runId, CancellationToken cancellationToken)
        => reports.SaveReportManifestAsync(facilityId, reportId, runId, cancellationToken);
}