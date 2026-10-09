using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class KafkaBrowseRequest
{
    public string Topic { get; init; } = "";
    public string Mode { get; init; } = "newest";
    public IReadOnlyList<int> Partitions { get; init; } = [];
    public long? Offset { get; init; }
    public long? TimestampUnixMs { get; init; }
    public int Limit { get; init; } = KafkaBrowseLimits.DefaultLimit;
    public string Key { get; init; } = "";
    public string HeaderName { get; init; } = "";
    public string HeaderValue { get; init; } = "";
    public int? ByteBudget { get; init; }
}

public sealed class KafkaBrowseBusyException : KafkaOpsRejectedException
{
    public KafkaBrowseBusyException()
        : base("Another message browse is already running. Wait for it to finish.")
    {
    }
}

public sealed class KafkaBrowseBrokerException : Exception
{
    public KafkaBrowseBrokerException(string message) : base(message) { }
}

public interface IKafkaMessageBrowser
{
    Task<KafkaBrowsePage> ReadAsync(KafkaBrowseRequest request, CancellationToken cancellationToken);
    Task<KafkaBrowsePage> ExportAsync(ClaimsPrincipal user, KafkaBrowseRequest request, CancellationToken cancellationToken);
    Task<KafkaFamilyView> FamilyAsync(string topic, CancellationToken cancellationToken);
}

public sealed class KafkaMessageBrowser : IKafkaMessageBrowser
{
    private readonly IKafkaBrowseSessionFactory _sessions;
    private readonly IKafkaBrokerGateway _gateway;
    private readonly IMigrationStore? _store;
    private readonly IProducer<string, AuditEventMessage>? _audit;
    private readonly ILogger<KafkaMessageBrowser> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public KafkaMessageBrowser(
        IKafkaBrowseSessionFactory sessions,
        IKafkaBrokerGateway gateway,
        ILogger<KafkaMessageBrowser> logger,
        IEnumerable<IProducer<string, AuditEventMessage>> audit,
        IMigrationStore? store = null)
    {
        _sessions = sessions;
        _gateway = gateway;
        _logger = logger;
        _store = store;
        _audit = audit.FirstOrDefault();
    }

    public Task<KafkaBrowsePage> ReadAsync(KafkaBrowseRequest request, CancellationToken cancellationToken) =>
        ReadCoreAsync(request, audit: false, user: null, cancellationToken);

    public async Task<KafkaBrowsePage> ExportAsync(ClaimsPrincipal user, KafkaBrowseRequest request, CancellationToken cancellationToken)
    {
        var page = await ReadCoreAsync(request, audit: false, user: null, cancellationToken);
        await AuditExportAsync(user, page, cancellationToken);
        return page;
    }

    public async Task<KafkaFamilyView> FamilyAsync(string topic, CancellationToken cancellationToken)
    {
        var backups = await BackupNamesAsync(cancellationToken);
        var admission = KafkaBrowseAllowList.Admit(topic, backups);
        if (!admission.Allowed)
            throw new KafkaOpsRejectedException(admission.Reason);

        var members = KafkaBrowseAllowList.MembersOf(admission.Main.Length == 0 ? admission.Topic : admission.Main).ToList();
        if (members.Count == 0)
            return new KafkaFamilyView { Main = admission.Main, Error = "That topic has no family." };

        var described = await _gateway.DescribeTopicsAsync(members.Select(member => member.Topic).ToList(), probeAlter: false, cancellationToken);
        var byName = described.ToDictionary(row => row.Topic, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<GroupView>? groups;
        var lagKnown = true;
        try
        {
            groups = await _gateway.DescribeGroupsAsync(false, 1, cancellationToken);
        }
        catch (Exception ex)
        {
            lagKnown = false;
            groups = null;
            _logger.LogWarning(ex, "Kafka family lag could not be read for {Topic}", admission.Main.SanitizeAndRemove());
        }

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byName.TryGetValue(member.Topic, out var row) || !string.IsNullOrEmpty(row.Error) || row.Partitions <= 0)
            {
                member.Exists = false;
                member.LagKnown = lagKnown;
                continue;
            }

            member.Exists = true;
            member.Partitions = row.Partitions;
            member.HighWatermarkSum = row.HighWatermarks.Sum();
            member.LagKnown = lagKnown;
            if (lagKnown && groups is not null)
            {
                var catalogGroups = KafkaTopicCatalog.GroupsOf(member.Main);
                member.Lag = groups
                    .Where(group => catalogGroups.Contains(group.GroupId, StringComparer.Ordinal))
                    .Sum(group => group.Partitions.Where(partition => string.Equals(partition.Topic, member.Topic, StringComparison.Ordinal)).Sum(partition => partition.Lag));
            }
        }

        return new KafkaFamilyView { Main = admission.Main, Members = members };
    }

    private async Task<KafkaBrowsePage> ReadCoreAsync(KafkaBrowseRequest request, bool audit, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(request, cancellationToken);
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new KafkaBrowseBusyException();

        var session = _sessions.Open();
        try
        {
            var page = ReadSession(session, prepared, cancellationToken);
            if (audit && user is not null)
                await AuditExportAsync(user, page, cancellationToken);
            _logger.LogInformation(
                "Kafka message browse of {Topic} mode {Mode} returned {Count}",
                prepared.Topic.SanitizeAndRemove(),
                prepared.Mode,
                page.Metadata.Returned);
            return page;
        }
        catch (KafkaBrowseBrokerException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Kafka message browse of {Topic} failed", prepared.Topic.SanitizeAndRemove());
            throw new KafkaBrowseBrokerException(BrokerSentence(ex));
        }
        finally
        {
            session.Dispose();
            _gate.Release();
        }
    }

    private KafkaBrowsePage ReadSession(IKafkaBrowseSession session, PreparedBrowse prepared, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(KafkaBrowseLimits.Timeout);
        var started = Stopwatch.StartNew();
        List<KafkaBrowsePartition> described;
        try
        {
            described = session.Describe(prepared.Topic).ToList();
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Kafka message browse of {Topic} failed", prepared.Topic.SanitizeAndRemove());
            throw new KafkaBrowseBrokerException(BrokerSentence(ex));
        }

        if (described.Count == 0)
            throw new KafkaOpsRejectedException("That topic is not on the broker.");
        if (described.Count > KafkaBrowseLimits.MaxPartitions && prepared.Partitions.Count == 0)
            throw new KafkaOpsRejectedException("This topic has too many partitions to browse at once. Choose at most " + KafkaBrowseLimits.MaxPartitions + ".");

        var selected = prepared.Partitions.Count == 0
            ? described
            : described.Where(partition => prepared.Partitions.Contains(partition.Id)).ToList();
        var missing = prepared.Partitions.Where(id => described.All(partition => partition.Id != id)).ToList();
        if (missing.Count > 0)
            throw new KafkaOpsRejectedException("Partition " + missing[0] + " is not on that topic.");

        var seeks = new List<KafkaBrowseSeek>();
        foreach (var partition in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = StartOffset(session, prepared, partition);
            if (start is null || start.Value >= partition.High)
                continue;
            seeks.Add(new KafkaBrowseSeek { Topic = prepared.Topic, Partition = partition.Id, Offset = start.Value });
        }

        var collected = new List<KafkaBrowseRecord>();
        var capHit = false;
        var scanned = 0;
        var bytes = 0;
        if (seeks.Count > 0)
        {
            try
            {
                session.AssignAndSeek(seeks);
            }
            catch (KafkaException ex)
            {
                _logger.LogWarning(ex, "Kafka message browse of {Topic} failed", prepared.Topic.SanitizeAndRemove());
                throw new KafkaBrowseBrokerException(BrokerSentence(ex));
            }

            var open = seeks.Select(seek => seek.Partition).ToHashSet();
            var newest = prepared.Mode == "newest";
            while (open.Count > 0 && (newest || collected.Count < prepared.Limit))
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
                if (timeout.IsCancellationRequested)
                {
                    capHit = true;
                    break;
                }

                KafkaBrowsePolled? polled;
                try
                {
                    polled = session.Poll(TimeSpan.FromMilliseconds(250));
                }
                catch (KafkaException ex)
                {
                    _logger.LogWarning(ex, "Kafka message browse of {Topic} failed", prepared.Topic.SanitizeAndRemove());
                    throw new KafkaBrowseBrokerException(BrokerSentence(ex));
                }

                if (polled is null)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);
                    if (timeout.IsCancellationRequested)
                        capHit = true;
                    break;
                }

                if (polled.EndOfPartition || !open.Contains(polled.Partition))
                {
                    open.Remove(polled.Partition);
                    continue;
                }

                scanned++;
                var size = polled.Value?.Length ?? 0;
                if (scanned > KafkaBrowseLimits.MaxScanned || bytes >= prepared.ByteBudget)
                {
                    capHit = true;
                    break;
                }

                bytes += size;
                if (!Matches(polled, prepared))
                {
                    if (bytes > prepared.ByteBudget)
                    {
                        capHit = true;
                        break;
                    }

                    continue;
                }

                var truncated = false;
                var shown = polled.Value ?? [];
                if (bytes > prepared.ByteBudget)
                {
                    var over = bytes - prepared.ByteBudget;
                    var keep = Math.Max(0, shown.Length - over);
                    shown = Prefix(shown, keep);
                    truncated = true;
                    capHit = true;
                }

                collected.Add(ToRecord(prepared.Topic, polled, shown, size, truncated));
                if (capHit)
                    break;
            }
        }

        if (prepared.Mode == "newest")
        {
            collected = collected
                .OrderByDescending(record => record.TimestampUnixMs)
                .ThenByDescending(record => record.Offset)
                .Take(prepared.Limit)
                .ToList();
        }

        return new KafkaBrowsePage
        {
            Topic = prepared.Topic,
            Mode = prepared.Mode,
            Records = collected,
            Metadata = new KafkaBrowseMetadata
            {
                Returned = collected.Count,
                Truncated = capHit || collected.Any(record => record.Truncated),
                CapHit = capHit,
                ElapsedMs = started.ElapsedMilliseconds
            }
        };
    }

    private static long? StartOffset(IKafkaBrowseSession session, PreparedBrowse prepared, KafkaBrowsePartition partition)
    {
        if (partition.High <= partition.Low)
            return null;
        if (prepared.Mode == "oldest")
            return partition.Low;
        if (prepared.Mode == "from-offset")
        {
            var offset = prepared.Offset ?? partition.Low;
            if (offset < partition.Low)
                return partition.Low;
            return offset;
        }

        if (prepared.Mode == "since")
        {
            var at = session.OffsetForTime(prepared.Topic, partition.Id, prepared.TimestampUnixMs ?? 0);
            if (at is null)
                return null;
            return Math.Max(partition.Low, at.Value);
        }

        var window = Math.Max(prepared.Limit, 1);
        return Math.Max(partition.Low, partition.High - window);
    }

    private static bool Matches(KafkaBrowsePolled polled, PreparedBrowse prepared)
    {
        if (prepared.Key.Length > 0)
        {
            if (!KafkaBrowseDecoder.TryUtf8(polled.Key, out var key) || !key.Contains(prepared.Key, StringComparison.Ordinal))
                return false;
        }

        if (prepared.HeaderName.Length == 0 || prepared.HeaderValue.Length == 0)
            return true;

        return polled.Headers.Any(header =>
            string.Equals(header.Name, prepared.HeaderName, StringComparison.OrdinalIgnoreCase)
            && HeaderText(header).Contains(prepared.HeaderValue, StringComparison.Ordinal));
    }

    private static KafkaBrowseRecord ToRecord(string topic, KafkaBrowsePolled polled, byte[] shown, int fullSize, bool truncated)
    {
        var keyOk = KafkaBrowseDecoder.TryUtf8(polled.Key, out var keyText);
        var valueOk = KafkaBrowseDecoder.TryUtf8(shown, out var valueText);
        var headers = polled.Headers.Select(header => new KafkaBrowseHeader
        {
            Name = header.Name,
            Value = HeaderText(header)
        }).ToList();
        var value = valueOk ? valueText : KafkaBrowseDecoder.HexPreview(shown);
        return new KafkaBrowseRecord
        {
            Partition = polled.Partition,
            Offset = polled.Offset,
            TimestampUnixMs = polled.TimestampUnixMs,
            Key = keyOk ? keyText : KafkaBrowseDecoder.HexPreview(polled.Key ?? []),
            KeyNote = keyOk ? null : "The key is not valid UTF-8.",
            Value = value,
            ValueSummary = KafkaBrowseDecoder.Summary(value),
            ValuePretty = truncated || !valueOk ? null : KafkaBrowseDecoder.Pretty(valueText),
            Truncated = truncated,
            ByteSize = fullSize,
            Headers = headers,
            Link = KafkaBrowseDecoder.Decode(topic, keyOk ? keyText : null, valueOk && !truncated ? valueText : null, headers)
        };
    }

    private static string HeaderText(KafkaBrowseHeaderBytes header)
    {
        if (KafkaBrowseDecoder.TryUtf8(header.Value, out var text))
            return text;
        return KafkaBrowseDecoder.HexPreview(header.Value);
    }

    private static byte[] Prefix(byte[] value, int take)
    {
        if (take >= value.Length)
            return value;
        if (take <= 0)
            return [];
        while (take > 0 && take < value.Length && (value[take] & 0xC0) == 0x80)
            take--;
        return value[..Math.Max(take, 0)];
    }

    private async Task<PreparedBrowse> PrepareAsync(KafkaBrowseRequest request, CancellationToken cancellationToken)
    {
        var mode = (request.Mode ?? "").Trim().ToLowerInvariant();
        if (mode is not ("newest" or "oldest" or "from-offset" or "since"))
            throw new KafkaOpsRejectedException("Mode must be newest, oldest, from-offset, or since.");
        if (request.Limit < 1 || request.Limit > KafkaBrowseLimits.MaxLimit)
            throw new KafkaOpsRejectedException("Limit must be from 1 to 50.");
        var budget = request.ByteBudget ?? KafkaBrowseLimits.MaxPageBytes;
        if (budget < 1 || budget > KafkaBrowseLimits.MaxPageBytes)
            throw new KafkaOpsRejectedException("The byte budget is over the cap of " + KafkaBrowseLimits.MaxPageBytes + ".");
        if (request.Partitions.Count > KafkaBrowseLimits.MaxPartitions)
            throw new KafkaOpsRejectedException("Choose at most " + KafkaBrowseLimits.MaxPartitions + " partitions.");
        if (request.Partitions.Any(partition => partition < 0))
            throw new KafkaOpsRejectedException("A partition id cannot be negative.");
        if (mode == "from-offset" && request.Offset is null)
            throw new KafkaOpsRejectedException("From offset needs an offset.");
        if (mode == "since" && request.TimestampUnixMs is null)
            throw new KafkaOpsRejectedException("Since time needs a timestamp.");

        var backups = await BackupNamesAsync(cancellationToken);
        var admission = KafkaBrowseAllowList.Admit(request.Topic, backups);
        if (!admission.Allowed)
            throw new KafkaOpsRejectedException(admission.Reason);

        return new PreparedBrowse
        {
            Topic = admission.Topic,
            Mode = mode,
            Partitions = request.Partitions.Distinct().ToList(),
            Offset = request.Offset,
            TimestampUnixMs = request.TimestampUnixMs,
            Limit = request.Limit,
            Key = request.Key ?? "",
            HeaderName = request.HeaderName ?? "",
            HeaderValue = request.HeaderValue ?? "",
            ByteBudget = budget
        };
    }

    private async Task<HashSet<string>> BackupNamesAsync(CancellationToken cancellationToken)
    {
        if (_store is null)
            return new HashSet<string>(StringComparer.Ordinal);
        var records = await _store.ListAsync(cancellationToken);
        return records
            .Select(record => record.BackupTopic)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task AuditExportAsync(ClaimsPrincipal user, KafkaBrowsePage page, CancellationToken cancellationToken)
    {
        var who = KafkaOpsService.UserName(user);
        if (_audit is null)
        {
            _logger.LogInformation("Kafka message export for {Topic} has no audit producer", page.Topic.SanitizeAndRemove());
            return;
        }

        var message = new AuditEventMessage
        {
            ServiceName = "LinkAdminBFF",
            CorrelationId = Guid.NewGuid().ToString("N"),
            EventDate = DateTime.UtcNow,
            User = who,
            UserId = who,
            Action = AuditEventType.Query,
            Resource = page.Topic,
            Notes = "export " + page.Mode + " " + page.Metadata.Returned,
            PropertyChanges =
            [
                new PropertyChangeModel("topic", "", page.Topic),
                new PropertyChangeModel("mode", "", page.Mode),
                new PropertyChangeModel("count", "", page.Metadata.Returned.ToString())
            ]
        };

        try
        {
            var headers = new Headers();
            headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(message.CorrelationId ?? ""));
            await _audit.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Key = page.Topic,
                Value = message,
                Headers = headers
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka message export audit was not produced for {Topic}", page.Topic.SanitizeAndRemove());
        }
    }

    private static string BrokerSentence(KafkaException ex)
    {
        var reason = (ex.Error.Reason ?? "").SanitizeAndRemove();
        if (reason.Length > 180)
            reason = reason[..180];
        return reason.Length == 0 ? "The broker refused the read." : "The broker refused the read. " + reason;
    }

    private sealed class PreparedBrowse
    {
        public string Topic { get; init; } = "";
        public string Mode { get; init; } = "";
        public List<int> Partitions { get; init; } = [];
        public long? Offset { get; init; }
        public long? TimestampUnixMs { get; init; }
        public int Limit { get; init; }
        public string Key { get; init; } = "";
        public string HeaderName { get; init; } = "";
        public string HeaderValue { get; init; } = "";
        public int ByteBudget { get; init; }
    }
}
