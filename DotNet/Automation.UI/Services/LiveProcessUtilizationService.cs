namespace Automation.UI.Services;

public sealed record LiveProcessUtilizationResponse(
    bool Reachable,
    DateTimeOffset SampledAt,
    IReadOnlyList<LiveProcessUtilizationItem> Services);

public sealed record LiveProcessUtilizationItem(
    string Key,
    string Name,
    string Group,
    double? CpuPercent,
    double? CpuCores,
    double? MemoryBytes,
    double? ApiP95Ms);

public interface ILiveProcessUtilizationService
{
    Task<LiveProcessUtilizationResponse> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class LiveProcessUtilizationService : ILiveProcessUtilizationService
{
    private static readonly string[] PipelineOrder =
    [
        "DataAcquisition",
        "DataAcquisitionWorker",
        "Normalization",
        "Report",
        "measureeval",
        "ValidationService",
        "Submission"
    ];

    private readonly IPrometheusHistogramClient _prometheus;
    private readonly TimeProvider _time;

    public LiveProcessUtilizationService(IPrometheusHistogramClient prometheus, TimeProvider? time = null)
    {
        _prometheus = prometheus;
        _time = time ?? TimeProvider.System;
    }

    public async Task<LiveProcessUtilizationResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var sampledAt = _time.GetUtcNow();
        if (!await _prometheus.IsReachableAsync(cancellationToken))
            return new LiveProcessUtilizationResponse(false, sampledAt, []);

        var ramTask = _prometheus.QueryVectorAsync(
            "sum by (exported_job) (process_memory_usage_bytes)", cancellationToken: cancellationToken);
        var cpuTask = _prometheus.QueryVectorAsync(
            "sum by (exported_job) (rate(process_cpu_time_seconds_total[1m]))", cancellationToken: cancellationToken);
        var cpuCountTask = _prometheus.QueryVectorAsync(
            "avg by (exported_job) (process_cpu_count)", cancellationToken: cancellationToken);
        var heapTask = _prometheus.QueryVectorAsync(
            "sum by (exported_job) (jvm_memory_used_bytes{jvm_memory_type=\"heap\"})", cancellationToken: cancellationToken);
        var jvmRatioTask = _prometheus.QueryVectorAsync(
            "avg by (exported_job) (jvm_cpu_recent_utilization_ratio)", cancellationToken: cancellationToken);
        var jvmCountTask = _prometheus.QueryVectorAsync(
            "avg by (exported_job) (jvm_cpu_count)", cancellationToken: cancellationToken);
        var apiTask = _prometheus.QueryVectorAsync(
            "histogram_quantile(0.95, sum by (le, exported_job) (rate(http_server_request_duration_seconds_bucket{http_route!~\"/health|/api/health|/hubs/.*\"}[1m])))",
            cancellationToken: cancellationToken);

        await Task.WhenAll(ramTask, cpuTask, cpuCountTask, heapTask, jvmRatioTask, jvmCountTask, apiTask);

        var ram = ToMap(ramTask.Result);
        var cpu = ToMap(cpuTask.Result);
        var cpuCount = ToMap(cpuCountTask.Result);
        var heap = ToMap(heapTask.Result);
        var jvmRatio = ToMap(jvmRatioTask.Result);
        var jvmCount = ToMap(jvmCountTask.Result);
        var apiP95 = ToMap(apiTask.Result);
        var cpuPercent = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (job, cores) in cpu)
            cpuPercent[job] = ToTaskManagerPercent(cores, cpuCount.GetValueOrDefault(job));

        foreach (var (job, ratio) in jvmRatio)
        {
            cpuPercent[job] = ratio * 100.0;
            if (jvmCount.TryGetValue(job, out var count) && count > 0)
                cpu[job] = ratio * count;
            else if (!cpu.ContainsKey(job))
                cpu[job] = ratio;
        }

        foreach (var (job, bytes) in heap)
        {
            if (!ram.ContainsKey(job) || ram[job] <= 0)
                ram[job] = bytes;
        }

        var jobs = ram.Keys.Concat(cpu.Keys).Concat(apiP95.Keys)
            .Where(j => !string.IsNullOrWhiteSpace(j) && !string.Equals(j, "otel-collector", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(PipelineRank)
            .ThenBy(DisplayName)
            .ToList();

        var services = jobs.Select(job => new LiveProcessUtilizationItem(
            job,
            DisplayName(job),
            PipelineRank(job) < PipelineOrder.Length ? "pipeline" : "platform",
            cpuPercent.TryGetValue(job, out var percent) ? percent : null,
            cpu.TryGetValue(job, out var cores) ? cores : null,
            ram.TryGetValue(job, out var bytes) ? bytes : null,
            apiP95.TryGetValue(job, out var seconds) ? seconds * 1000.0 : null)).ToList();

        return new LiveProcessUtilizationResponse(true, sampledAt, services);
    }

    internal static string DisplayName(string exportedJob) => exportedJob switch
    {
        "DataAcquisition" => "Data Acquisition",
        "DataAcquisitionWorker" => "DA worker",
        "Normalization" => "Normalization",
        "Report" => "Report",
        "measureeval" => "Measure Evaluation",
        "ValidationService" => "Validation",
        "Submission" => "Submission",
        "QueryDispatch" => "Query Dispatch",
        "LinkAdminBFF" => "Admin BFF",
        "AutomationUI" => "Automation UI",
        _ => exportedJob
    };

    internal static double ToTaskManagerPercent(double cores, double cpuCount) =>
        cpuCount <= 0 ? 0 : 100.0 * cores / cpuCount;

    private static int PipelineRank(string exportedJob)
    {
        var index = Array.FindIndex(PipelineOrder, j => string.Equals(j, exportedJob, StringComparison.Ordinal));
        return index < 0 ? PipelineOrder.Length : index;
    }

    private static Dictionary<string, double> ToMap(IReadOnlyList<PromSample> samples)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.ExportedJob))
                continue;
            map[sample.ExportedJob] = sample.Value;
        }

        return map;
    }
}
