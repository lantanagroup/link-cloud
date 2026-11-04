using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.LinkSdk;

public class ValidationServiceClientTests
{
    [Fact]
    public async System.Threading.Tasks.Task CategoryIssuesPreservesEmptyPageAndPagingParameters()
    {
        const string page = "{\"records\":[],\"metadata\":{\"pageNumber\":2,\"pageSize\":50,\"totalCount\":0,\"totalPages\":0}}";
        using var http = new FakeHttpBoundary(page);
        using var client = Create(http.BaseUrl);
        var response = await client.GetCategoryIssuesAsync("facility", "report", "category", 2, 50);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(page, response.Body);
        var request = http.SingleRequest();
        Assert.Equal("/api/validation/facilities/facility/reports/report/categories/category/issues", request.Path);
        Assert.Contains("pageNumber=2", request.Query);
        Assert.Contains("pageSize=50", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task CsvCopiesIntoDestinationAndLeavesItOpen()
    {
        const string csv = "patientId,message\r\npatient,finding\r\n";
        using var http = new FakeHttpBoundary(csv);
        using var client = Create(http.BaseUrl);
        using var destination = new MemoryStream();
        var id = Guid.NewGuid();
        var response = await client.CopyValidationResultsCsvAsync("facility", id, destination);
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(destination.CanWrite);
        Assert.Equal(csv, System.Text.Encoding.UTF8.GetString(destination.ToArray()));
        Assert.Equal($"/api/validation/facilities/facility/reports/{id:D}/results.csv", http.SingleRequest().Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task CsvPreservesProblemResponseWithoutWritingItToDestination()
    {
        const string problem = "{\"status\":400,\"detail\":\"Invalid Id format\"}";
        using var http = new FakeHttpBoundary(problem, 400);
        using var client = Create(http.BaseUrl);
        using var destination = new MemoryStream();
        var response = await client.CopyValidationResultsCsvAsync("facility", Guid.NewGuid(), destination);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(problem, response.RawBody);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async System.Threading.Tasks.Task CsvHonorsCancellationBeforeSendingRequest()
    {
        using var http = new FakeHttpBoundary("");
        using var client = Create(http.BaseUrl);
        using var destination = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CopyValidationResultsCsvAsync("facility", Guid.NewGuid(), destination, cancellation.Token));
    }

    private static ValidationServiceClient Create(string url) => new(
        Options.Create(new ServiceRegistry { ValidationServiceUrl = url }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);
}
