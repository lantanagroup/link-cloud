using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// Where Link.UI reads a submitted patient file. This is the external container
/// Submission copies into, not the automation container behind InternalBlobStorage.
/// </summary>
public sealed class SubmissionReadOptions
{
    public const string Section = "SubmissionRead";

    public string? ExternalConnectionString { get; set; }
    public string? ExternalContainerName { get; set; }
    public string? ExternalBlobRoot { get; set; }

    /// <summary>Report's internal blob root, stripped when naming the external file. Not the automation root.</summary>
    public string? InternalBlobRoot { get; set; }

    public bool FlattenHierarchy { get; set; }
    public bool UseMeasurePrefix { get; set; }
    public Dictionary<string, string>? MeasurePrefixesByReportType { get; set; }
}

public sealed record SubmissionBlobAddress(string Container, string Name);

/// <summary>
/// Names the external <c>patient-{id}.ndjson</c> the same way Submission copies it.
/// </summary>
public static class SubmissionBlobNames
{
    public static bool TryPatientFile(string? patientId, out string fileName)
    {
        fileName = string.Empty;
        var id = (patientId ?? string.Empty).Trim();
        if (id.Length is 0 or > 80 || id is "." or "..")
            return false;

        foreach (var ch in id)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.')
                continue;
            return false;
        }

        fileName = "patient-" + id + ".ndjson";
        return true;
    }

    public static string? ExternalName(SubmissionReadOptions options, ReportScheduleApiModel schedule, string fileName)
    {
        string? internalName = null;
        if (!string.IsNullOrWhiteSpace(schedule.PayloadRootUri)
            && Uri.TryCreate(schedule.PayloadRootUri.Trim(), UriKind.Absolute, out var uri))
        {
            var folder = new BlobUriBuilder(uri).BlobName.Trim('/');
            if (folder.Length > 0)
                internalName = folder + "/" + fileName;
        }

        if (internalName is null)
        {
            try
            {
                var reportName = ReportHelpers.GetReportName(
                    schedule.Id,
                    schedule.FacilityId,
                    schedule.ReportTypes ?? [],
                    schedule.ReportStartDate);
                internalName = Join(options.InternalBlobRoot, reportName, fileName);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return ToExternal(options, internalName, schedule.ReportTypes);
    }

    private static string ToExternal(SubmissionReadOptions options, string internalName, IReadOnlyList<string>? reportTypes)
    {
        var blobName = internalName;
        var root = options.InternalBlobRoot?.Trim('/');
        if (!string.IsNullOrEmpty(root) && blobName.StartsWith(root, StringComparison.Ordinal))
            blobName = blobName[root.Length..];

        blobName = blobName.Trim('/');
        if (options.FlattenHierarchy)
        {
            var index = blobName.LastIndexOf('/');
            if (index >= 0)
                blobName = blobName[..index] + "_" + blobName[(index + 1)..];
        }

        return Join(options.ExternalBlobRoot, MeasurePrefix(options, reportTypes), blobName);
    }

    private static string? MeasurePrefix(SubmissionReadOptions options, IReadOnlyList<string>? reportTypes)
    {
        if (!options.UseMeasurePrefix || reportTypes is null)
            return null;

        if (options.MeasurePrefixesByReportType is not null)
        {
            foreach (var type in reportTypes)
            {
                if (options.MeasurePrefixesByReportType.TryGetValue(type, out var mapped)
                    && !string.IsNullOrWhiteSpace(mapped))
                    return mapped.Trim().Trim('/');
            }
        }

        foreach (var type in reportTypes)
        {
            if (!string.IsNullOrWhiteSpace(type))
                return type.Trim().Trim('/');
        }

        return null;
    }

    private static string Join(params string?[] segments)
    {
        var kept = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
                continue;
            var trimmed = segment.Trim().Trim('/');
            if (trimmed.Length > 0)
                kept.Add(trimmed);
        }

        return string.Join('/', kept);
    }
}

public interface ISubmissionBlobStore
{
    bool IsConfigured { get; }

    Task<Stream?> OpenAsync(string blobName, CancellationToken cancellationToken);

    Task<string?> ReadRangeAsync(string blobName, long offset, int length, CancellationToken cancellationToken);
}

public sealed class AzureSubmissionBlobStore : ISubmissionBlobStore
{
    private readonly SubmissionReadOptions _options;
    private readonly object _gate = new();
    private BlobContainerClient? _client;

    public AzureSubmissionBlobStore(IOptions<SubmissionReadOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ExternalConnectionString)
        && !string.IsNullOrWhiteSpace(_options.ExternalContainerName);

    public async Task<Stream?> OpenAsync(string blobName, CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(blobName))
            return null;

        try
        {
            var blob = Client().GetBlobClient(blobName);
            return await blob.OpenReadAsync(new BlobOpenReadOptions(false) { BufferSize = 256 * 1024 }, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<string?> ReadRangeAsync(string blobName, long offset, int length, CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(blobName) || offset < 0 || length <= 0)
            return null;

        var take = Math.Min(length, 1_000_000);
        try
        {
            var blob = Client().GetBlobClient(blobName);
            var result = await blob.DownloadStreamingAsync(
                new BlobDownloadOptions { Range = new HttpRange(offset, take) },
                cancellationToken);
            await using var stream = result.Value.Content;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private BlobContainerClient Client()
    {
        if (_client is not null)
            return _client;

        lock (_gate)
        {
            _client ??= new BlobContainerClient(_options.ExternalConnectionString, _options.ExternalContainerName);
            return _client;
        }
    }
}

/// <summary>
/// Streams one submitted patient file. A missing file returns null so the caller can use MeasureEval.
/// </summary>
public sealed class PatientSubmissionReader
{
    private readonly SubmissionReadOptions _options;
    private readonly ISubmissionBlobStore _store;
    private readonly ILogger<PatientSubmissionReader> _logger;

    public PatientSubmissionReader(
        IOptions<SubmissionReadOptions> options,
        ISubmissionBlobStore store,
        ILogger<PatientSubmissionReader> logger)
    {
        _options = options.Value;
        _store = store;
        _logger = logger;
    }

    public bool IsConfigured => _store.IsConfigured;

    public async Task<ResourceGraphIndex?> ReadAsync(
        ReportScheduleApiModel schedule,
        string patientId,
        Func<CancellationToken, Task>? onOpened,
        Func<int, CancellationToken, ValueTask>? onProgress,
        CancellationToken cancellationToken)
    {
        if (!_store.IsConfigured || !SubmissionBlobNames.TryPatientFile(patientId, out var fileName))
            return null;

        var name = SubmissionBlobNames.ExternalName(_options, schedule, fileName);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        Stream? stream;
        try
        {
            stream = await _store.OpenAsync(name, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The submitted patient file could not be opened.");
            return null;
        }

        if (stream is null)
            return null;

        if (onOpened is not null)
            await onOpened(cancellationToken);

        await using (stream)
        {
            var index = await ResourceGraphRules.ReadNdjsonAsync(stream, patientId.Trim(), cancellationToken, onProgress: onProgress);
            index.Address = new SubmissionBlobAddress(_options.ExternalContainerName ?? string.Empty, name);
            return index;
        }
    }

    public Task<string?> ReadBodyAsync(SubmissionBlobAddress address, long offset, int length, CancellationToken cancellationToken)
    {
        if (!_store.IsConfigured || string.IsNullOrWhiteSpace(address.Name))
            return Task.FromResult<string?>(null);

        return _store.ReadRangeAsync(address.Name, offset, length, cancellationToken);
    }
}
