using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Application.Services.ReportActivity;

public sealed class NormalizationReportSummary
{
    public string ReportId { get; set; } = "";
    public int Passes { get; set; }
    public long ResourcesNormalized { get; set; }
    public long Failures { get; set; }
    public DateTime? FirstNormalizedUtc { get; set; }
    public DateTime? LastNormalizedUtc { get; set; }
}

public interface INormalizationReportActivityStore
{
    Task RecordAsync(string facilityId, IEnumerable<string> reportIds, string correlationId, string queryType, int resources, int failures, CancellationToken cancellationToken);
    Task<NormalizationReportSummary> SummaryAsync(string reportId, CancellationToken cancellationToken);
}

public sealed class NormalizationReportActivityStore : INormalizationReportActivityStore
{
    public const int KeyLength = 255;
    private readonly NormalizationDbContext _db;

    public NormalizationReportActivityStore(NormalizationDbContext db) => _db = db;

    public static string Key(string? value) => (value ?? "").Trim() is var v && v.Length > KeyLength ? v[..KeyLength] : (value ?? "").Trim();

    public async Task RecordAsync(string facilityId, IEnumerable<string> reportIds, string correlationId, string queryType, int resources, int failures, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var correlation = Key(correlationId);
        var query = Key(queryType);
        var facility = Key(facilityId);
        foreach (var reportId in reportIds.Select(Key).Where(id => id.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Set, not add: a redelivered pass rewrites its own row.
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
MERGE NormalizationReportActivities WITH (HOLDLOCK) AS t
USING (SELECT {reportId} AS ReportTrackingId, {correlation} AS CorrelationId, {query} AS QueryType) AS s
ON t.ReportTrackingId = s.ReportTrackingId AND t.CorrelationId = s.CorrelationId AND t.QueryType = s.QueryType
WHEN MATCHED THEN UPDATE SET Resources = {resources}, Failures = {failures}, FacilityId = {facility}, UpdatedUtc = {now}
WHEN NOT MATCHED THEN INSERT (ReportTrackingId, CorrelationId, QueryType, FacilityId, Resources, Failures, CreatedUtc, UpdatedUtc)
VALUES ({reportId}, {correlation}, {query}, {facility}, {resources}, {failures}, {now}, {now});", cancellationToken);
        }
    }

    public async Task<NormalizationReportSummary> SummaryAsync(string reportId, CancellationToken cancellationToken)
    {
        var id = Key(reportId);
        // One aggregate over the clustered key range for this report.
        var row = await _db.NormalizationReportActivities
            .Where(a => a.ReportTrackingId == id)
            .GroupBy(a => a.ReportTrackingId)
            .Select(g => new
            {
                Passes = g.Count(),
                Resources = g.Sum(a => (long)a.Resources),
                Failures = g.Sum(a => (long)a.Failures),
                First = g.Min(a => (DateTime?)a.CreatedUtc),
                Last = g.Max(a => (DateTime?)a.UpdatedUtc)
            })
            .FirstOrDefaultAsync(cancellationToken);
        return new NormalizationReportSummary
        {
            ReportId = id,
            Passes = row?.Passes ?? 0,
            ResourcesNormalized = row?.Resources ?? 0,
            Failures = row?.Failures ?? 0,
            FirstNormalizedUtc = row?.First,
            LastNormalizedUtc = row?.Last
        };
    }
}