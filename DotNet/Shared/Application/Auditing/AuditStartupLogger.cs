using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Confirms the service can publish audit events, then writes what its audit events mask to the
    /// log - once, at startup.
    /// </summary>
    /// <remarks>
    /// Masking is the mechanism's one safety guarantee, and a property that should have been masked
    /// and was not is published in clear before anyone notices - so the effective list goes in the
    /// service's own startup log, rather than being spread across a shared default, a registration
    /// callback and an attribute on an entity.
    /// <para>
    /// The check builds the publisher once, so a service that registered auditing without a Kafka
    /// connection fails to start with the line to add, instead of failing every audited request in
    /// production. Building the producer starts the client's connection attempts but does not wait
    /// for them, so a broker that is merely down does not stop startup; only a missing
    /// configuration does. A test host with its own publisher needs no Kafka.
    /// </para>
    /// </remarks>
    public sealed class AuditStartupLogger : IHostedService
    {
        private readonly AuditOptions _options;
        private readonly ServiceInformation _serviceInformation;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AuditStartupLogger> _logger;

        public AuditStartupLogger(IOptions<AuditOptions> options, ServiceInformation serviceInformation,
            IServiceScopeFactory scopes, ILogger<AuditStartupLogger> logger)
        {
            _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _serviceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            string attributes;

            //the publisher is scoped, like the requests that use it, and so is the context.
            using (var scope = _scopes.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IAuditPublisher>();
                attributes = AttributeMasks(scope.ServiceProvider);
            }

            var service = string.IsNullOrWhiteSpace(_serviceInformation.ServiceConfigName)
                ? _serviceInformation.ServiceName
                : _serviceInformation.ServiceConfigName;

            _logger.LogInformation(
                "Audit masking for {ServiceName}: names=[{MaskedNames}] patterns=[{MaskedPatterns}] attributes=[{MaskedAttributes}] skipped timestamps=[{TimestampNames}]",
                service,
                string.Join(", ", _options.MaskedPropertyNames),
                string.Join(", ", _options.MaskedPropertyPatterns),
                attributes,
                string.Join(", ", _options.TimestampPropertyNames));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Every mapped property the service's entities mark <see cref="AuditMaskAttribute"/>, as
        /// <c>Entity.Property</c>, read from the model the interceptor sees.
        /// </summary>
        /// <remarks>
        /// A service registered without a context audits no entities, so it has none. Building the
        /// model does not connect to the database; if it cannot be built at all, the line says so
        /// rather than claiming there are none, and the service still starts.
        /// </remarks>
        private string AttributeMasks(IServiceProvider services)
        {
            try
            {
                var context = services.GetService<DbContext>();
                if (context is null)
                {
                    return string.Empty;
                }

                var masked = context.Model.GetEntityTypes()
                    .SelectMany(entityType => entityType.GetProperties()
                        .Where(property => property.PropertyInfo?.IsDefined(typeof(AuditMaskAttribute), inherit: true) == true)
                        .Select(property => $"{entityType.ClrType.Name}.{property.Name}"))
                    .Distinct()
                    .Order(StringComparer.Ordinal);

                return string.Join(", ", masked);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "The [AuditMask] properties could not be read from the service's model.");
                return "unreadable";
            }
        }
    }
}
