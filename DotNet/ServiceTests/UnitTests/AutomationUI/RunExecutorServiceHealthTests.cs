using System.Net;
using Automation.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunExecutorServiceHealthTests
{
    [Fact]
    public async Task CheckHealthAsync_Http200_ReturnsTrue()
    {
        using var client = CreateClient(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await RunExecutor.CheckHealthAsync(
            client,
            "https://terminology.example.com/health",
            CancellationToken.None);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CheckHealthAsync_NonSuccessStatus_ReturnsFalse(
        HttpStatusCode statusCode)
    {
        using var client = CreateClient(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(statusCode)));

        var result = await RunExecutor.CheckHealthAsync(
            client,
            "https://terminology.example.com/health",
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_HttpRequestException_ReturnsFalse()
    {
        using var client = CreateClient(
            (_, _) => Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Connection refused")));

        var result = await RunExecutor.CheckHealthAsync(
            client,
            "https://terminology.example.com/health",
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_RequestTimeout_ReturnsFalse()
    {
        using var client = CreateClient(
            (_, _) => Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("Request timed out")));

        var result = await RunExecutor.CheckHealthAsync(
            client,
            "https://terminology.example.com/health",
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_CallerCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();

        using var client = CreateClient(
            (_, token) =>
            {
                cancellation.Cancel();

                return Task.FromCanceled<HttpResponseMessage>(
                    token);
            });

        Func<Task> action = async () =>
            await RunExecutor.CheckHealthAsync(
                client,
                "https://terminology.example.com/health",
                cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void BuildMonitoredServiceHealthUrls_ConstructsExpectedUrls()
    {
        var configuration = CreateConfiguration(
            ("ServiceRegistry:TerminologyServiceUrl",
                "https://terminology.example.com/"),
            ("ServiceRegistry:ValidationServiceUrl",
                "https://validation.example.com"));

        var services = RunExecutor.BuildMonitoredServiceHealthUrls(
            configuration);

        services.Should().HaveCount(2);

        services.Should().Contain(service =>
            service.Name == "Terminology" &&
            service.Url == "https://terminology.example.com/health");

        services.Should().Contain(service =>
            service.Name == "Validation" &&
            service.Url == "https://validation.example.com/health");
    }

    [Fact]
    public void BuildMonitoredServiceHealthUrls_MissingUrls_ReturnsEmptyList()
    {
        var configuration = CreateConfiguration();

        var services = RunExecutor.BuildMonitoredServiceHealthUrls(
            configuration);

        services.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://terminology.example.com")]
    public void BuildMonitoredServiceHealthUrls_InvalidUrl_Throws(
        string serviceUrl)
    {
        var configuration = CreateConfiguration(
            ("ServiceRegistry:TerminologyServiceUrl", serviceUrl));

        Action action = () =>
            RunExecutor.BuildMonitoredServiceHealthUrls(
                configuration);

        action.Should().Throw<ArgumentException>();
    }

    private static IConfiguration CreateConfiguration(
        params (string Key, string Value)[] values)
    {
        var configurationValues = values.ToDictionary(
            value => value.Key,
            value => (string?)value.Value);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> response)
    {
        return new HttpClient(new StubHttpMessageHandler(response));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return response(request, cancellationToken);
        }
    }
}