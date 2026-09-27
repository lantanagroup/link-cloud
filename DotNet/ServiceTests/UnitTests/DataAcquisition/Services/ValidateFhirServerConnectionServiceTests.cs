using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using UnitTests.Admin.BFF.Aggregation;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services;

[Trait("Category", "UnitTests")]
public class ValidateFhirServerConnectionServiceTests
{
    private const string FhirServerUrl = "http://fhir.test/r4";

    private static CapabilityStatement BuildCapabilityStatement() => new()
    {
        Status = PublicationStatus.Active,
        FhirVersion = FHIRVersion.N4_0_1,
        Format = ["json"],
        Kind = CapabilityStatementKind.Instance,
        Software = new CapabilityStatement.SoftwareComponent { Name = "Test FHIR Server", Version = "1.0" }
    };

    private static HttpResponseMessage FhirResponse(HttpStatusCode statusCode, Resource resource) =>
        new(statusCode)
        {
            Content = new StringContent(new FhirJsonSerializer().SerializeToString(resource), Encoding.UTF8, "application/fhir+json")
        };

    private static ValidateFhirServerConnectionService BuildSut(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        out Mock<IHttpClientFactory> httpClientFactory)
    {
        httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new MockHttpMessageHandler(handler)));

        return new ValidateFhirServerConnectionService(
            Mock.Of<ILogger<ValidateFhirServerConnectionService>>(),
            httpClientFactory.Object);
    }

    private static ValidateFhirServerConnectionService BuildSut(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        BuildSut(handler, out _);

    [Fact]
    public async Task ValidateConnection_ServerReturnsCapabilityStatement_ReturnsConnected()
    {
        var sut = BuildSut(_ => FhirResponse(HttpStatusCode.OK, BuildCapabilityStatement()));

        var result = await sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = FhirServerUrl }, CancellationToken.None);

        Assert.True(result.IsConnected);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("http://fhir.test/r4")]
    [InlineData("http://fhir.test/r4/")]
    [InlineData("  http://fhir.test/r4/  ")]
    public async Task ValidateConnection_RequestsMetadataEndpoint_WithoutDoubleSlash(string suppliedUrl)
    {
        HttpRequestMessage? captured = null;
        var sut = BuildSut(req =>
        {
            captured = req;
            return FhirResponse(HttpStatusCode.OK, BuildCapabilityStatement());
        });

        await sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = suppliedUrl }, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("http://fhir.test/r4/metadata", captured!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Contains(captured.Headers.Accept, h => h.MediaType == "application/fhir+json");
    }

    [Fact]
    public async Task ValidateConnection_UsesNamedFhirHttpClient()
    {
        var sut = BuildSut(_ => FhirResponse(HttpStatusCode.OK, BuildCapabilityStatement()), out var httpClientFactory);

        await sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = FhirServerUrl }, CancellationToken.None);

        httpClientFactory.Verify(x => x.CreateClient("FhirHttpClient"), Times.Once);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ValidateConnection_MetadataReturnsNonSuccess_ReturnsNotConnectedWithErrorMessage(HttpStatusCode statusCode)
    {
        var sut = BuildSut(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(string.Empty)
        });

        var result = await sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = FhirServerUrl }, CancellationToken.None);

        Assert.False(result.IsConnected);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(((int)statusCode).ToString(), result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateConnection_MetadataReturnsDifferentResourceType_ReturnsNotConnected()
    {
        var operationOutcome = new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.NotSupported,
                    Diagnostics = "metadata is not supported"
                }
            ]
        };

        var sut = BuildSut(_ => FhirResponse(HttpStatusCode.OK, operationOutcome));

        var result = await sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = FhirServerUrl }, CancellationToken.None);

        Assert.False(result.IsConnected);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("CapabilityStatement", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateConnection_MetadataReturnsNonFhirBody_ReturnsNotConnected()
    {
        var sut = BuildSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Hello</body></html>", Encoding.UTF8, "text/html")
        });

        var result = await sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = FhirServerUrl }, CancellationToken.None);

        Assert.False(result.IsConnected);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateConnection_TransportFailure_ThrowsFhirConnectionFailedException()
    {
        var sut = BuildSut(_ => throw new HttpRequestException("No such host is known."));

        var ex = await Assert.ThrowsAsync<FhirConnectionFailedException>(() =>
            sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = FhirServerUrl }, CancellationToken.None));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task ValidateConnection_CallerCancels_PropagatesCancellationRatherThanConnectionFailure()
    {
        var sut = BuildSut(_ => FhirResponse(HttpStatusCode.OK, BuildCapabilityStatement()));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = FhirServerUrl }, cts.Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateConnection_MissingUrl_ThrowsBadRequestException(string? url)
    {
        var sut = BuildSut(_ => FhirResponse(HttpStatusCode.OK, BuildCapabilityStatement()));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ValidateConnection(new ValidateFhirServerConnectionRequest { FhirServerUrl = url }, CancellationToken.None));
    }
}
