using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// An auditable endpoint in a host that never called AddLinkAuditing - a hand-built test host is
    /// the usual one - fails every request. The error has to say which line is missing, not just
    /// that DI could not build AuditableFilter.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class AuditRegistrationMissingTests
    {
        private static readonly AuditableAttribute UpdateSftp = new(AuditEventType.Update, "SftpConfiguration");

        [Fact]
        public void AControllerAction_SaysWhichRegistrationIsMissing()
        {
            using var provider = new ServiceCollection().BuildServiceProvider();

            var ex = Assert.Throws<InvalidOperationException>(() => UpdateSftp.CreateInstance(provider));

            Assert.Contains("AddLinkAuditing", ex.Message);
            Assert.Contains("SftpConfiguration", ex.Message);
        }

        [Fact]
        public void AControllerAction_GetsItsFilterWhenAuditingIsRegistered()
        {
            var services = new ServiceCollection();
            services.AddSingleton(AuditTest.Recorder(new AuditContext(), new FakeAuditPublisher()));
            using var provider = services.BuildServiceProvider();

            Assert.IsType<AuditableFilter>(UpdateSftp.CreateInstance(provider));
        }

        [Fact]
        public async Task AMappedEndpoint_SaysWhichRegistrationIsMissingAndRunsNothing()
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().BuildServiceProvider()
            };
            var filter = new AuditableEndpointFilter(AuditEventType.SoftDelete, "LinkUser");
            var handlerRan = false;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
                {
                    handlerRan = true;
                    return ValueTask.FromResult<object?>(Results.NoContent());
                }));

            Assert.Contains("AddLinkAuditing", ex.Message);
            Assert.Contains("LinkUser", ex.Message);
            Assert.False(handlerRan);
        }
    }
}
