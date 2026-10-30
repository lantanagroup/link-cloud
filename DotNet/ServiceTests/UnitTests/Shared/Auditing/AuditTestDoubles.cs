using System.Security.Claims;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Claim = System.Security.Claims.Claim;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// Records what was published instead of reaching Kafka.
    /// </summary>
    internal sealed class FakeAuditPublisher : IAuditPublisher
    {
        public List<AuditEventMessage> Published { get; } = [];

        public Task PublishAsync(AuditEventMessage message, CancellationToken cancellationToken)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Keeps every message logged at or above <see cref="MinimumLevel"/>, so a test can see what a
    /// developer turning on that level would see.
    /// </summary>
    internal sealed class CapturingLogger<T>(LogLevel minimumLevel = LogLevel.Debug) : ILogger<T>
    {
        public LogLevel MinimumLevel { get; } = minimumLevel;

        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }

    internal sealed class FakeAuditMetrics : IAuditMetrics
    {
        public List<string?> Failures { get; } = [];

        public void PublishFailed(string? resource) => Failures.Add(resource);
    }

    internal sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    internal static class AuditTest
    {
        public const string ConfiguredServiceName = "Tenant";

        public const string DisplayServiceName = "Link Tenant Service";

        public static ServiceInformation Service() => new()
        {
            ServiceName = DisplayServiceName,
            ServiceConfigName = ConfiguredServiceName
        };

        public static AuditOptions Options(Action<AuditOptions>? configure = null)
        {
            var options = new AuditOptions();
            configure?.Invoke(options);
            return options;
        }

        public static IOptions<AuditOptions> AsOptions(this AuditOptions options) =>
            Microsoft.Extensions.Options.Options.Create(options);

        public static AuditEventFactory Factory(TimeProvider? timeProvider = null, ServiceInformation? service = null,
            ILogger<AuditEventFactory>? logger = null) =>
            new(service ?? Service(), timeProvider ?? TimeProvider.System, logger ?? NullLogger<AuditEventFactory>.Instance);

        public static AuditRecorder Recorder(IAuditContext audit, IAuditPublisher publisher,
            IAuditEventFactory? factory = null, IAuditMetrics? metrics = null,
            ILogger<AuditRecorder>? logger = null) =>
            new(audit, factory ?? Factory(), publisher, metrics ?? new FakeAuditMetrics(),
                new TestHostApplicationLifetime(), logger ?? NullLogger<AuditRecorder>.Instance);

        /// <summary>
        /// A signed-in caller whose token carries the given email claim.
        /// </summary>
        public static ClaimsPrincipal SignedInAs(string email) =>
            new(new ClaimsIdentity(
                [
                    new Claim(LinkAuthorizationConstants.LinkSystemClaims.Email, email),
                    new Claim(LinkAuthorizationConstants.LinkSystemClaims.Subject, "9f0d1b4e")
                ],
                authenticationType: "TestBearer"));

        /// <summary>
        /// A token with a subject but no email claim, which is what a service-to-service call will
        /// look like once LEGLINK-1359 stops CreateSystemToken minting the admin address.
        /// </summary>
        public static ClaimsPrincipal SignedInWithoutEmail() =>
            new(new ClaimsIdentity(
                [new Claim(LinkAuthorizationConstants.LinkSystemClaims.Subject,
                    LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount)],
                authenticationType: "TestBearer"));

        public static ClaimsPrincipal NobodySignedIn() => new(new ClaimsIdentity());
    }

    /// <summary>
    /// Serialises each event exactly as the Kafka producer would, so a test can look at what would
    /// actually have left the service rather than at the object that was handed over.
    /// </summary>
    internal sealed class SerialisingAuditPublisher : IAuditPublisher
    {
        public List<string> PublishedJson { get; } = [];

        public Task PublishAsync(AuditEventMessage message, CancellationToken cancellationToken)
        {
            PublishedJson.Add(System.Text.Json.JsonSerializer.Serialize(message));
            return Task.CompletedTask;
        }
    }
}
