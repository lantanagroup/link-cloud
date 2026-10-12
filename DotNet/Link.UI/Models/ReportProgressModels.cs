namespace Link.UI.Models;

public enum PipelineState { Waiting, Active, Done, Unavailable }

public sealed record PipelineCount(string Label, long Value, string? BadgeClass = null);

public sealed class PipelineRow
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public PipelineState State { get; set; }
    public List<PipelineCount> Counts { get; } = [];
    public long Pending { get; set; }
    public DateTime? LastActivityUtc { get; set; }

    public string StateLabel => State switch
    {
        PipelineState.Active => "In progress",
        PipelineState.Done => "Done",
        PipelineState.Unavailable => "Not available",
        _ => "Waiting"
    };

    public string StateBadge => State switch
    {
        PipelineState.Active => "au-badge-warning",
        PipelineState.Done => "au-badge-success",
        _ => "au-badge-muted"
    };
}

public sealed record ProgressSlice(string Name, int Count, string Color);

public sealed class MeasureProgress
{
    public string Measure { get; init; } = "";
    public int? Patients { get; init; }
    public int? Evaluated { get; init; }
    public int? InitialPopulation { get; init; }
}

public sealed class ReportProgress
{
    public bool Terminal { get; set; }
    public string Stage { get; set; } = "";
    public string StageBadge { get; set; } = "au-badge-muted";
    public DateTime? LastActivityUtc { get; set; }
    public List<PipelineRow> Pipeline { get; } = [];
    public List<ProgressSlice> Submission { get; } = [];
    public bool SubmissionAvailable { get; set; }
    public List<MeasureProgress> Measures { get; } = [];
    public bool MeasuresAvailable { get; set; }
    public bool PopulationAvailable { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime? CreatedUtc { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public DateTime TimeToSubmitFromUtc { get; set; }
    public TimeSpan? TimeToSubmit { get; set; }
    public bool TimeToSubmitRunning { get; set; }
    public List<string> Unavailable { get; set; } = [];

    public string SubmissionGradient
    {
        get
        {
            var total = Submission.Sum(s => s.Count);
            if (total == 0) return "var(--au-warning) 0 100%";
            var parts = new List<string>();
            double at = 0;
            foreach (var slice in Submission)
            {
                var next = at + slice.Count * 100.0 / total;
                parts.Add($"{slice.Color} {at:0.###}% {next:0.###}%");
                at = next;
            }
            return string.Join(", ", parts);
        }
    }
}
public sealed class RunReportWatch
{
    public Guid RunId { get; init; }
    public string? ReportHref { get; init; }
    /// <summary>The run ended; no report is coming, so stop watching.</summary>
    public bool RunEnded { get; init; }
    public static bool Ended(string? status) => status is "Succeeded" or "Failed" or "Cancelled";
}
