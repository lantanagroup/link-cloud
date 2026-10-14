using System.Net.Http.Headers;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.KafkaAdmin.Migration;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;

/// <summary>
/// Topic migrations run in the Operations service. The integration producers here ask it which topics are held
/// before they produce or consume. When Operations is configured but cannot be read, every topic counts as held,
/// so a test message never lands on a topic that is mid-migration.
/// </summary>
public sealed class OperationsMigrationHolds : IMigrationHoldRegistry
{
    public const string HttpClientName = "OperationsService";
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _clients;
    private readonly IOptions<ServiceRegistry> _registry;
    private readonly IOptions<AuthenticationSchemaConfig> _authentication;
    private readonly IHttpContextAccessor _context;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OperationsMigrationHolds> _logger;
    private readonly object _gate = new();
    private HashSet<string> _held = new(StringComparer.Ordinal);
    private DateTimeOffset _readAt = DateTimeOffset.MinValue;
    private bool _unreadable;

    public OperationsMigrationHolds(
        IHttpClientFactory clients,
        IOptions<ServiceRegistry> registry,
        IOptions<AuthenticationSchemaConfig> authentication,
        IHttpContextAccessor context,
        IServiceScopeFactory scopes,
        ILogger<OperationsMigrationHolds> logger)
    {
        _clients = clients;
        _registry = registry;
        _authentication = authentication;
        _context = context;
        _scopes = scopes;
        _logger = logger;
    }

    public bool IsHeld(string topic)
    {
        lock (_gate)
            return _unreadable || _held.Contains(topic);
    }

    public async Task<bool> IsHeldAsync(string topic, CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        return IsHeld(topic);
    }

    public Guid? MigrationFor(string topic) => null;

    /// <summary>Holds are owned by the Operations service; nothing in the BFF sets them.</summary>
    public void Set(string topic, Guid migrationId, bool held)
    {
    }

    public IReadOnlyCollection<string> HeldTopics()
    {
        lock (_gate)
            return _held.ToList();
    }

    public async Task<IReadOnlyCollection<string>> HeldTopicsAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        return HeldTopics();
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (DateTimeOffset.UtcNow - _readAt < Fresh)
                return;
        }

        var baseUrl = _registry.Value.OperationsServiceUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Store([], unreadable: false);
            return;
        }

        try
        {
            var client = _clients.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd('/') + "/api/ops/kafka/holds");
            var user = _context.HttpContext?.User;
            if (!_authentication.Value.EnableAnonymousAccess && user is not null)
            {
                using var scope = _scopes.CreateScope();
                var token = await scope.ServiceProvider.GetRequiredService<ICreateLinkBearerToken>().ExecuteAsync(user, 2, cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var topics = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken) ?? [];
            Store(topics, unreadable: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Kafka topic holds could not be read from the Operations service. Every topic counts as held until they can.");
            Store([], unreadable: true);
        }
    }

    private void Store(IEnumerable<string> topics, bool unreadable)
    {
        lock (_gate)
        {
            _held = new HashSet<string>(topics, StringComparer.Ordinal);
            _unreadable = unreadable;
            _readAt = DateTimeOffset.UtcNow;
        }
    }
}
