using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps.Protocol;

/// <summary>Partition reassignment admin calls that Confluent.Kafka (librdkafka) does not provide.</summary>
public interface IKafkaReassignmentAdmin
{
    Task<IReadOnlyList<OngoingReassignment>> ListAsync(CancellationToken cancellationToken);

    Task<ReassignmentOutcome> AlterAsync(IReadOnlyList<PartitionTarget> targets, CancellationToken cancellationToken);
}

/// <summary>
/// Settings for the admin wire client, taken from the same KafkaConnection shape every Link service uses
/// (BootstrapServers, SaslProtocolEnabled, Protocol, Mechanism, SaslUsername, SaslPassword).
/// </summary>
public sealed class KafkaWireSettings
{
    public List<(string Host, int Port)> Servers { get; init; } = [];
    public bool Tls { get; init; }
    public string? SaslMechanism { get; init; }
    public string User { get; init; } = "";
    public string Password { get; init; } = "";
    public string ClientId { get; init; } = "LinkAdminBFF-reassign";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Why this connection cannot be used, or empty. Reported on each call rather than at startup.</summary>
    public string Problem { get; init; } = "";

    public static KafkaWireSettings From(KafkaConnection connection)
    {
        var protocol = connection.SaslProtocolEnabled ? connection.Protocol : SecurityProtocol.Plaintext;
        string? mechanism = null;
        var problem = "";
        if (protocol is SecurityProtocol.SaslPlaintext or SecurityProtocol.SaslSsl)
        {
            mechanism = connection.Mechanism switch
            {
                Confluent.Kafka.SaslMechanism.Plain => "PLAIN",
                Confluent.Kafka.SaslMechanism.ScramSha256 => "SCRAM-SHA-256",
                Confluent.Kafka.SaslMechanism.ScramSha512 => "SCRAM-SHA-512",
                _ => null
            };
            if (mechanism is null)
                problem = "KafkaConnection:Mechanism " + connection.Mechanism + " is not supported for partition reassignment. PLAIN, SCRAM-SHA-256 and SCRAM-SHA-512 are.";
        }

        return new KafkaWireSettings
        {
            Servers = ParseServers(connection.BootstrapServers),
            Tls = protocol is SecurityProtocol.Ssl or SecurityProtocol.SaslSsl,
            SaslMechanism = mechanism,
            User = connection.SaslUsername ?? "",
            Password = connection.SaslPassword ?? "",
            Problem = problem
        };
    }

    /// <summary>Accepts "host:port", "SASL_PLAINTEXT://host:port", trailing slashes and comma-joined entries.</summary>
    public static List<(string Host, int Port)> ParseServers(IEnumerable<string>? entries)
    {
        var servers = new List<(string, int)>();
        foreach (var raw in (entries ?? []).SelectMany(entry => (entry ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            var text = raw.Trim().TrimEnd('/');
            var scheme = text.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
                text = text[(scheme + 3)..];
            var colon = text.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(text[(colon + 1)..], out var port) || port is <= 0 or > 65535)
                continue;
            servers.Add((text[..colon].Trim('[', ']'), port));
        }

        return servers;
    }
}

/// <summary>
/// A small Kafka protocol client for AlterPartitionReassignments and ListPartitionReassignments (KIP-455).
/// It connects with plaintext or TLS, signs in with SASL PLAIN or SCRAM, checks the broker supports both requests,
/// and follows NOT_CONTROLLER to the controller on ZooKeeper clusters (KRaft brokers forward them on their own).
/// One short-lived connection per call keeps it free of shared socket state.
/// </summary>
public sealed class KafkaAdminWire : IKafkaReassignmentAdmin
{
    private const int MaxResponseBytes = 64 * 1024 * 1024;
    private readonly KafkaWireSettings _settings;

    public KafkaAdminWire(KafkaWireSettings settings) => _settings = settings;

    public async Task<IReadOnlyList<OngoingReassignment>> ListAsync(CancellationToken cancellationToken)
    {
        var body = KafkaAdminMessages.ListPartitionReassignmentsRequest((int)_settings.Timeout.TotalMilliseconds);
        return await CallControllerAsync<List<OngoingReassignment>>(KafkaAdminMessages.ListPartitionReassignmentsKey, body, reader =>
        {
            try
            {
                return (KafkaAdminMessages.ReadListPartitionReassignments(reader), KafkaAdminMessages.ErrorNone);
            }
            catch (KafkaAdminProtocolException ex) when (ex.Code == KafkaAdminMessages.ErrorNotController)
            {
                return ((List<OngoingReassignment>?)null, ex.Code);
            }
        }, cancellationToken);
    }

    public async Task<ReassignmentOutcome> AlterAsync(IReadOnlyList<PartitionTarget> targets, CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
            return new ReassignmentOutcome();
        var body = KafkaAdminMessages.AlterPartitionReassignmentsRequest(targets, (int)_settings.Timeout.TotalMilliseconds);
        var outcome = await CallControllerAsync<ReassignmentOutcome>(KafkaAdminMessages.AlterPartitionReassignmentsKey, body, reader =>
        {
            var read = KafkaAdminMessages.ReadAlterPartitionReassignments(reader);
            return read.ErrorCode == KafkaAdminMessages.ErrorNotController ? ((ReassignmentOutcome?)null, read.ErrorCode) : (read, KafkaAdminMessages.ErrorNone);
        }, cancellationToken);
        if (outcome.ErrorCode != KafkaAdminMessages.ErrorNone)
            throw new KafkaAdminProtocolException(outcome.ErrorCode, "Kafka refused the reassignment. " + outcome.ErrorMessage);
        return outcome;
    }

    private async Task<T> CallControllerAsync<T>(short apiKey, byte[] body, Func<KafkaWireReader, (T? Result, short Error)> read, CancellationToken cancellationToken)
        where T : class
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_settings.Timeout + _settings.Timeout);
        var token = budget.Token;
        await using var connection = await ConnectAnyAsync(_settings.Servers, token);
        var first = read(await connection.SendAsync(apiKey, 0, true, true, body, token));
        if (first.Result is not null)
            return first.Result;

        // ZooKeeper mode: the request must go to the controller.
        var metadata = KafkaAdminMessages.ReadMetadata(await connection.SendAsync(KafkaAdminMessages.MetadataKey, 1, false, false, KafkaAdminMessages.MetadataRequest(), token));
        var controller = metadata.Brokers.FirstOrDefault(broker => broker.Id == metadata.ControllerId);
        if (controller.Host is null)
            throw new KafkaAdminProtocolException(KafkaAdminMessages.ErrorNotController, "Kafka did not report a reachable controller.");
        await using var direct = await ConnectAnyAsync([(controller.Host, controller.Port)], token);
        var second = read(await direct.SendAsync(apiKey, 0, true, true, body, token));
        return second.Result ?? throw new KafkaAdminProtocolException(second.Error, "The controller moved while the request was sent. Try again.");
    }

    private async Task<WireConnection> ConnectAnyAsync(IReadOnlyList<(string Host, int Port)> servers, CancellationToken cancellationToken)
    {
        if (_settings.Problem.Length > 0)
            throw new KafkaWireException(_settings.Problem);
        if (servers.Count == 0)
            throw new KafkaWireException("KafkaConnection:BootstrapServers has no usable host:port.");
        Exception? last = null;
        foreach (var (host, port) in servers)
        {
            try
            {
                var connection = await WireConnection.OpenAsync(host, port, _settings, cancellationToken);
                try
                {
                    await connection.PrepareAsync(cancellationToken);
                    return connection;
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            }
            catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or KafkaWireException or System.Security.Authentication.AuthenticationException)
            {
                last = ex;
            }
        }

        throw new KafkaWireException("No Kafka bootstrap server accepted the admin connection. " + last?.Message, last!);
    }

    private sealed class WireConnection : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly Stream _stream;
        private readonly KafkaWireSettings _settings;
        private int _correlation;
        private Dictionary<short, (short Min, short Max)> _versions = [];

        private WireConnection(TcpClient client, Stream stream, KafkaWireSettings settings)
        {
            _client = client;
            _stream = stream;
            _settings = settings;
        }

        public static async Task<WireConnection> OpenAsync(string host, int port, KafkaWireSettings settings, CancellationToken cancellationToken)
        {
            var client = new TcpClient { NoDelay = true };
            try
            {
                await client.ConnectAsync(host, port, cancellationToken);
                Stream stream = client.GetStream();
                if (settings.Tls)
                {
                    var tls = new SslStream(stream, false);
                    await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, cancellationToken);
                    stream = tls;
                }

                return new WireConnection(client, stream, settings);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public async Task PrepareAsync(CancellationToken cancellationToken)
        {
            _versions = KafkaAdminMessages.ReadApiVersions(await SendAsync(KafkaAdminMessages.ApiVersionsKey, 0, false, false, KafkaAdminMessages.ApiVersionsRequest(), cancellationToken));
            foreach (var key in new[] { KafkaAdminMessages.AlterPartitionReassignmentsKey, KafkaAdminMessages.ListPartitionReassignmentsKey })
            {
                if (!_versions.TryGetValue(key, out var range) || range.Min > 0)
                    throw new KafkaWireException("This Kafka cluster does not support partition reassignment requests (Kafka 2.4 or later is required).");
            }

            if (_settings.SaslMechanism is not null)
                await SignInAsync(_settings.SaslMechanism, cancellationToken);
        }

        private async Task SignInAsync(string mechanism, CancellationToken cancellationToken)
        {
            KafkaAdminMessages.ReadSaslHandshake(await SendAsync(KafkaAdminMessages.SaslHandshakeKey, 1, false, false, KafkaAdminMessages.SaslHandshakeRequest(mechanism), cancellationToken), mechanism);
            var authVersion = (short)(_versions.TryGetValue(KafkaAdminMessages.SaslAuthenticateKey, out var range) && range.Max >= 1 ? 1 : 0);
            if (mechanism == "PLAIN")
            {
                await AuthenticateAsync(authVersion, Encoding.UTF8.GetBytes("\0" + _settings.User + "\0" + _settings.Password), cancellationToken);
                return;
            }

            var scram = new KafkaScram(mechanism, _settings.User, _settings.Password);
            var serverFirst = await AuthenticateAsync(authVersion, scram.ClientFirst(), cancellationToken);
            var serverFinal = await AuthenticateAsync(authVersion, scram.ClientFinal(serverFirst), cancellationToken);
            scram.VerifyServerFinal(serverFinal);
        }

        private async Task<byte[]> AuthenticateAsync(short version, byte[] token, CancellationToken cancellationToken)
        {
            var reader = await SendAsync(KafkaAdminMessages.SaslAuthenticateKey, version, false, false, KafkaAdminMessages.SaslAuthenticateRequest(token), cancellationToken);
            if (version >= 1)
                return KafkaAdminMessages.ReadSaslAuthenticate(reader);
            var error = reader.Int16();
            var message = reader.String();
            var bytes = reader.Bytes() ?? [];
            if (error != KafkaAdminMessages.ErrorNone)
                throw new KafkaAdminProtocolException(error, "Kafka sign-in failed. " + (message ?? ""));
            return bytes;
        }

        public async Task<KafkaWireReader> SendAsync(short apiKey, short version, bool flexibleRequest, bool flexibleResponseHeader, byte[] body, CancellationToken cancellationToken)
        {
            var correlation = Interlocked.Increment(ref _correlation);
            var frame = KafkaAdminMessages.Frame(apiKey, version, correlation, _settings.ClientId, flexibleRequest, body);
            await _stream.WriteAsync(frame, cancellationToken);
            await _stream.FlushAsync(cancellationToken);

            var size = new byte[4];
            await _stream.ReadExactlyAsync(size, cancellationToken);
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(size);
            if (length is < 4 or > MaxResponseBytes)
                throw new KafkaWireException("The broker sent an invalid response size " + length + ".");
            var payload = new byte[length];
            await _stream.ReadExactlyAsync(payload, cancellationToken);
            return KafkaAdminMessages.OpenResponse(payload, correlation, flexibleResponseHeader);
        }

        public async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync();
            _client.Dispose();
        }
    }
}
