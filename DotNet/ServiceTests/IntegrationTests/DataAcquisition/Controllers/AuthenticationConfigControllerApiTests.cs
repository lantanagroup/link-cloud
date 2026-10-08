using DataAcquisition.Domain.Application.Models;
using Moq;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

/// <summary>
/// HTTP-pipeline tests for the authentication endpoints. The FHIR list configuration no longer
/// carries authentication, so its path value must be rejected before any action runs.
/// </summary>
[Trait("Category", "IntegrationTests")]
public class AuthenticationConfigControllerApiTests : IClassFixture<AuthenticationConfigApiFactory>
{
    private const string BasicAuthBody = "{\"authType\":\"Basic\",\"userName\":\"u\",\"password\":\"p\"}";

    private readonly AuthenticationConfigApiFactory _factory;

    public AuthenticationConfigControllerApiTests(AuthenticationConfigApiFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    [Theory]
    [InlineData("GET", "fhirQueryListConfiguration")]
    [InlineData("POST", "fhirQueryListConfiguration")]
    [InlineData("PUT", "fhirQueryListConfiguration")]
    [InlineData("DELETE", "fhirQueryListConfiguration")]
    [InlineData("GET", "1")]
    [InlineData("DELETE", "1")]
    public async Task AuthenticationEndpoints_ListConfigurationPathValue_Returns400AndTouchesNothing(string method,
                                                                                                    string pathValue)
    {
        _factory.QueryConfigurationManager.Reset();
        _factory.QueryConfigurationQueries.Reset();
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(method),
                                             $"/api/data/facility-1/{pathValue}/authentication");
        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent(BasicAuthBody, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("queryConfigurationTypePathParameter", body);
        _factory.QueryConfigurationManager.VerifyNoOtherCalls();
        _factory.QueryConfigurationQueries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAuthentication_QueryConfigurationPathValue_ReadsQueryConfiguration()
    {
        _factory.QueryConfigurationManager.Reset();
        _factory.QueryConfigurationQueries.Reset();
        _factory.QueryConfigurationQueries
            .Setup(x => x.GetAuthenticationConfigurationByFacilityId("facility-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationConfigurationModel { AuthType = "Basic" });
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/data/facility-1/fhirQueryConfiguration/authentication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.QueryConfigurationQueries.Verify(
            x => x.GetAuthenticationConfigurationByFacilityId("facility-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
