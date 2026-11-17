using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// A service that registers auditing but no Kafka connection would otherwise start normally and
    /// fail on the first request to an audited endpoint - in production, on a user's request, and on
    /// every request to it after that. The startup check moves that failure to deployment.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class AuditStartupCheckTests
    {
        private static ServiceCollection AService(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMetrics();
            services.AddSingleton(AuditTest.Service());
            services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
            services.AddLinkAuditing();
            configure?.Invoke(services);
            return services;
        }

        private static Task StartAudit(IServiceProvider provider) =>
            provider.GetServices<IHostedService>().OfType<AuditStartupLogger>().Single()
                .StartAsync(CancellationToken.None);

        [Fact]
        public async Task AServiceWithNoKafkaConnection_FailsToStartAndSaysWhatIsMissing()
        {
            await using var provider = AService().BuildServiceProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAudit(provider));

            Assert.Contains("KafkaConnection", ex.Message);
        }

        [Fact]
        public async Task AServiceWhoseProducerIsRegistered_StartsWithTheRealPublisher()
        {
            //the check resolves the service's own KafkaAuditPublisher and starts, so a broker that
            //is down does not stop deployment. The producer is mocked because building a real one
            //opens sockets; that leaves the registration factory's KafkaConnection branch untested,
            //as it cannot run without one.
            await using var provider = AService(services =>
                services.AddSingleton(Mock.Of<IProducer<string, AuditEventMessage>>())).BuildServiceProvider();

            await StartAudit(provider);
        }

        [Fact]
        public async Task ATestHostWithAFakePublisher_StartsWithoutKafka()
        {
            await using var provider = AService(services =>
                services.AddScoped<IAuditPublisher, FakeAuditPublisher>()).BuildServiceProvider();

            await StartAudit(provider);
        }
    }
}
