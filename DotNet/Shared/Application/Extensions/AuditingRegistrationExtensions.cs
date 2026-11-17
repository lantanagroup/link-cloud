using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LantanaGroup.Link.Shared.Application.Extensions
{
    /// <summary>
    /// Registers the shared audit mechanism in a service.
    /// </summary>
    public static class AuditingRegistrationExtensions
    {
        /// <summary>
        /// For a service whose writes go through Entity Framework.
        /// </summary>
        /// <remarks>
        /// The interceptor is registered here but attached by the service, because only the service
        /// knows how it builds its context - see <see cref="AddLinkAuditInterceptor"/>:
        /// <code>
        /// builder.Services.AddLinkAuditing&lt;TenantDbContext&gt;();
        /// builder.Services.AddDbContext&lt;TenantDbContext&gt;((sp, options) =&gt;
        ///     options.UseSqlServer(connectionString)
        ///            .AddInterceptors(new UpdateBaseEntityInterceptor())
        ///            .AddLinkAuditInterceptor(sp));
        /// </code>
        /// </remarks>
        /// <typeparam name="TContext">
        /// The context the audited endpoints write through, so the interceptor captures their property
        /// changes. A service with two contexts names the one its audited writes go through.
        /// </typeparam>
        public static IServiceCollection AddLinkAuditing<TContext>(this IServiceCollection services,
            Action<AuditOptions>? configure = null) where TContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(services);

            AddCore(services, configure);

            //"the request's DbContext", so the interceptor does not have to know which context this
            //service uses.
            services.AddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<TContext>());
            services.AddScoped<AuditPropertyChangeInterceptor>();

            return services;
        }

        /// <summary>
        /// Attaches the property-change interceptor to the context being configured, when
        /// <see cref="AddLinkAuditing{TContext}"/> has registered one, and does nothing when it has not.
        /// </summary>
        /// <remarks>
        /// Entity Framework calls only the interceptors handed to the options, so without this line
        /// every audit event carries no property changes and nothing reports why. A missing
        /// registration is tolerated, so it is safe in setup code shared with a process that does
        /// not audit - Data Acquisition's worker, and the shared AddSQLServerEF helper.
        /// </remarks>
        /// <param name="options">The options passed to the service's AddDbContext.</param>
        /// <param name="serviceProvider">The provider AddDbContext hands its options callback.</param>
        public static DbContextOptionsBuilder AddLinkAuditInterceptor(this DbContextOptionsBuilder options,
            IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var interceptor = serviceProvider.GetService<AuditPropertyChangeInterceptor>();
            return interceptor is null ? options : options.AddInterceptors(interceptor);
        }

        /// <summary>
        /// For a service that persists nothing through Entity Framework, such as Terminology. No
        /// interceptor is registered, so events from such a service carry no property changes.
        /// </summary>
        public static IServiceCollection AddLinkAuditing(this IServiceCollection services,
            Action<AuditOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            AddCore(services, configure);

            return services;
        }

        private static void AddCore(IServiceCollection services, Action<AuditOptions>? configure)
        {
            var options = services.AddOptions<AuditOptions>();
            if (configure is not null)
            {
                options.Configure(configure);
            }

            //the defaults are the floor: AddOnlyStringSet makes removing an entry throw, so only a
            //future change replacing the collection could empty this.
            options
                .Validate(configured => configured.MaskedPropertyNames.Count > 0,
                    "The shared audit masking lists are missing; secrets would reach the audit trail.")
                .Validate(configured => configured.Publish.MaxAttempts >= 1,
                    "Audit publishing needs at least one attempt.")
                .ValidateOnStart();

            services.TryAddSingleton(TimeProvider.System);

            services.AddScoped<IAuditContext, AuditContext>();
            services.AddScoped<IAuditEventFactory, AuditEventFactory>();
            services.AddScoped<AuditRecorder>();
            services.AddScoped<IAuditPublisher, KafkaAuditPublisher>();
            services.AddSingleton<IAuditMetrics, AuditMetrics>();
            services.AddHostedService<AuditStartupLogger>();

            //most services already register this producer for their own audit sender; those keep
            //theirs, and the rest get one without a line of their own.
            services.TryAddSingleton<IProducer<string, AuditEventMessage>>(serviceProvider =>
            {
                var kafkaConnection = serviceProvider.GetService<KafkaConnection>()
                    ?? throw new InvalidOperationException(
                        "Auditing publishes to Kafka, so the service must register a KafkaConnection: " +
                        "builder.Services.AddSingleton(builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>());");

                return new KafkaProducerFactory<string, AuditEventMessage>(kafkaConnection).CreateAuditEventProducer();
            });
        }
    }
}
