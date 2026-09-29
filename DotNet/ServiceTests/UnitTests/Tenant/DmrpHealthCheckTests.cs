using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant
{
    [Trait("Category", "UnitTests")]
    public class DmrpHealthCheckTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("https://dmrp.example")]
        public async Task Disabled_ReturnsHealthyWithoutCreatingClient(string? baseUrl)
        {
            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            var check = CreateCheck(factory.Object, false, baseUrl);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            factory.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task EnabledWithoutApiUrl_ReturnsHealthyWithoutCreatingClient(string? baseUrl)
        {
            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            var check = CreateCheck(factory.Object, true, baseUrl);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("DMRP API is not configured; health check skipped.", result.Description);
            factory.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("file:///local")]
        public async Task EnabledWithInvalidUrl_ReturnsUnhealthyWithoutCreatingClient(string? baseUrl)
        {
            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            var check = CreateCheck(factory.Object, true, baseUrl);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            factory.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("https://dmrp.example", HttpStatusCode.OK, HealthStatus.Healthy)]
        [InlineData("https://dmrp.example/", HttpStatusCode.ServiceUnavailable, HealthStatus.Unhealthy)]
        [InlineData("https://dmrp.example", HttpStatusCode.Unauthorized, HealthStatus.Healthy)]
        [InlineData("https://dmrp.example", HttpStatusCode.BadRequest, HealthStatus.Healthy)]
        [InlineData("https://dmrp.example", HttpStatusCode.NotFound, HealthStatus.Healthy)]
        [InlineData("https://dmrp.example", HttpStatusCode.InternalServerError, HealthStatus.Unhealthy)]
        public async Task Enabled_ReportsEndpointStatus(string baseUrl, HttpStatusCode status, HealthStatus expected)
        {
            Uri? requestedUri = null;
            HttpMethod? requestedMethod = null;
            var canBeCanceled = false;
            using var handler = new StubHandler((request, token) =>
            {
                requestedUri = request.RequestUri;
                requestedMethod = request.Method;
                canBeCanceled = token.CanBeCanceled;
                return Task.FromResult(new HttpResponseMessage(status));
            });
            using var client = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            factory.Setup(instance => instance.CreateClient(DmrpApiClient.HttpClientName)).Returns(client);

            var result = await CreateCheck(factory.Object, true, baseUrl).CheckHealthAsync(new HealthCheckContext());

            Assert.Equal("https://dmrp.example/msc?nhsnorgid=0", requestedUri?.AbsoluteUri);
            Assert.Equal(HttpMethod.Get, requestedMethod);
            Assert.True(canBeCanceled);
            Assert.Equal(expected, result.Status);
            factory.Verify(instance => instance.CreateClient(DmrpApiClient.HttpClientName), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RequestFailure_ReturnsUnhealthy(bool timeout)
        {
            using var handler = new StubHandler((request, token) => throw (timeout
                ? new TaskCanceledException("timeout")
                : new HttpRequestException("connection failed")));
            using var client = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(instance => instance.CreateClient(DmrpApiClient.HttpClientName)).Returns(client);

            var result = await CreateCheck(factory.Object, true, "https://dmrp.example")
                .CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }

        [Fact]
        public async Task CallerCancellation_IsPropagated()
        {
            using var cancellation = new CancellationTokenSource();
            using var handler = new StubHandler((request, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
            using var client = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(instance => instance.CreateClient(DmrpApiClient.HttpClientName)).Returns(client);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateCheck(factory.Object, true, "https://dmrp.example")
                    .CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
        }

        private static DmrpHealthCheck CreateCheck(IHttpClientFactory factory, bool enabled, string? baseUrl) =>
            new(factory, Options.Create(new DmrpSettings
            {
                Enabled = enabled,
                Api = new DmrpApiSettings { BaseUrl = baseUrl }
            }));

        private sealed class StubHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken) => respond(request, cancellationToken);
        }
    }
}