using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class DataAcquisitionServiceClientTests
{
    [Fact]
    public async System.Threading.Tasks.Task GetAcquisitionLogNotesAsync_CallsExpectedEndpoint()
    {
        using var server = new OneShotServer("[\"n1\",\"n2\"]");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetAcquisitionLogNotesAsync(42);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/data/acquisition-logs/42/notes", request.Path);
        Assert.Equal(2, result.Body!.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetReportStatisticsAsync_CallsExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetReportStatisticsAsync("r1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/data/acquisition-logs/report/r1/statistics", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProcessAcquisitionLogsBulkAsync_PostsIds()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var ids = new List<long> { 11, 22 };
        var callTask = client.ProcessAcquisitionLogsBulkAsync(ids);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/process-bulk", request.Path);
        Assert.Contains("11", request.Body);
        Assert.Contains("22", request.Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProcessAcquisitionLogAsync_CallsExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.ProcessAcquisitionLogAsync(7);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/7/process", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelAcquisitionLogsByFilterAsync_PostsFilterAndQueryString()
    {
        const string response = "{\"requested\":2,\"cancelled\":1,\"ineligible\":1}";
        using var server = new OneShotServer(response);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CancelAcquisitionLogsByFilterAsync(new { facilityId = "f1" }, minAgeHours: 0);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/cancel-by-filter", request.Path);
        Assert.Contains("minAgeHours=0", request.Query);
        Assert.Contains("facilityId", request.Body);
        Assert.NotNull(result.Body);
        Assert.Equal(2, result.Body.Requested);
        Assert.Equal(1, result.Body.Cancelled);
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelAcquisitionLogsBulkAsync_PostsIdsAndQueryString()
    {
        const string response = "{\"requested\":1,\"cancelled\":1,\"ineligible\":0}";
        using var server = new OneShotServer(response);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CancelAcquisitionLogsBulkAsync([99], minAgeHours: 3);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/cancel-bulk", request.Path);
        Assert.Contains("minAgeHours=3", request.Query);
        Assert.Contains("99", request.Body);
        Assert.NotNull(result.Body);
        Assert.Equal(1, result.Body.Requested);
        Assert.Equal(1, result.Body.Cancelled);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProcessAcquisitionLogsByFilterAsync_PostsFilterBody()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.ProcessAcquisitionLogsByFilterAsync(new { facilityId = "f2", reportId = "r2" });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/process-by-filter", request.Path);
        Assert.Contains("facilityId", request.Body);
        Assert.Contains("reportId", request.Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task RestoreLogsByFacilityAsync_PatchesExpectedEndpoint()
    {
        using var server = new OneShotServer("1");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.RestoreLogsByFacilityAsync("f1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PATCH", request.Method);
        Assert.Equal("/api/data/acquisition-logs/facility/f1/restore", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task RestoreLogsByReportTrackingIdAsync_PatchesExpectedEndpoint()
    {
        using var server = new OneShotServer("1");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.RestoreLogsByReportTrackingIdAsync("rpt-1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PATCH", request.Method);
        Assert.Equal("/api/data/acquisition-logs/report/rpt-1/restore", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteAcquisitionLogAsync_DeletesExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteAcquisitionLogAsync(99);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/data/acquisition-logs/99", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task SoftDeleteLogsByReportTrackingIdAsync_DeletesExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SoftDeleteLogsByReportTrackingIdAsync("rpt-2");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/data/acquisition-logs/report/rpt-2", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateFhirQueryConfigurationAsync_PutsToConfigurationEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateFhirQueryConfigurationAsync(new { facilityId = "f1", fhirServerBaseUrl = "https://ehr/fhir" });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/data/fhirQueryConfiguration", request.Path);
        Assert.Contains("f1", request.Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateFacilityConnectionAsync_GetsFacilityScopedValidateRoute()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.ValidateFacilityConnectionAsync("f1", patientId: "p1", measureId: "m1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/data/connectionValidation/f1/$validate", request.Path);
        Assert.Contains("patientId=p1", request.Query);
        Assert.Contains("measureId=m1", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateConnectionAsync_ReturnsSyntheticSuccessWithoutCallingServer()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var result = await client.ValidateConnectionAsync("https://ehr/fhir");

        Assert.True(result.IsSuccessStatusCode);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetFhirListConfigurationAsync_WithIncludePatientName_SetsQueryParam()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetFhirListConfigurationAsync("f1", includePatientName: true);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/data/f1/fhirQueryList", request.Path);
        Assert.Contains("includePatientName=True", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateFhirListConfigurationAsync_PutsToListEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateFhirListConfigurationAsync(new { facilityId = "f1" });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/data/fhirQueryList", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateQueryPlanAsync_PutsToQueryPlanEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateQueryPlanAsync("f1", new { planName = "p" });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/data/f1/QueryPlan", request.Path);
        Assert.Contains("planName", request.Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateOrganizationLocationConfigurationAsync_PutsToConfigRoute()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateOrganizationLocationConfigurationAsync("f1", new { description = "d" });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/data/location-config/facility/f1", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteOrganizationLocationConfigurationAsync_DeletesConfigRoute()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteOrganizationLocationConfigurationAsync("f1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/data/location-config/facility/f1", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateOrganizationLocationMappingAsync_PutsToMappingRoute()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateOrganizationLocationMappingAsync(7, new { locationName = "ICU" });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/data/location-mappings/7", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task TestSftpConnectionAdHocAsync_ReturnsSyntheticSuccessWithoutCallingServer()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var result = await client.TestSftpConnectionAdHocAsync(new { host = "h" }, includeFileContent: true);

        Assert.True(result.IsSuccessStatusCode);
        Assert.Contains("success", result.RawBody);
    }

    [Theory]
    [MemberData(nameof(SftpEndpointCalls))]
    public async System.Threading.Tasks.Task SftpMethods_CallControllerRoutes(
        string expectedMethod,
        string expectedPath,
        Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task> invoke)
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = invoke(client);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal(expectedMethod, request.Method);
        Assert.Equal(expectedPath, request.Path);
    }

    public static IEnumerable<object[]> SftpEndpointCalls()
    {
        yield return ["GET", "/api/data/org-1/sftp-configurations", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.GetOrganizationSftpConfigurationAsync("org-1"))];
        yield return ["POST", "/api/data/org-1/sftp-configurations", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.CreateSftpConfigurationAsync("org-1", new { host = "h" }))];
        yield return ["PUT", "/api/data/org-1/sftp-configurations/cfg-1", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.UpdateSftpConfigurationAsync("org-1", "cfg-1", new { host = "h" }))];
        yield return ["DELETE", "/api/data/org-1/sftp-configurations/cfg-1", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.DeleteSftpConfigurationAsync("org-1", "cfg-1"))];
        yield return ["PUT", "/api/data/org-1/sftp-configurations/credentials", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.UpdateSftpCredentialsAsync("org-1", new { username = "u", password = "p" }))];
        yield return ["DELETE", "/api/data/org-1/sftp-configurations/credentials", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.DeleteSftpCredentialsAsync("org-1"))];
        yield return ["GET", "/api/data/org-1/sftp-configurations/credentials/status", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.GetSftpCredentialStatusAsync("org-1"))];
        yield return ["POST", "/api/data/org-1/sftp-configurations/test-connection", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.TestSftpConnectionAsync("org-1"))];
        yield return ["GET", "/api/data/sftp-logs", new Func<DataAcquisitionServiceClient, System.Threading.Tasks.Task>(async c =>
            await c.SearchSftpLogsAsync(facilityId: "f1"))];
    }

    private static DataAcquisitionServiceClient CreateClient(string baseUrl)
    {
        var serviceRegistry = Options.Create(new ServiceRegistry
        {
            DataAcquisitionServiceUrl = baseUrl
        });

        var bearerOptions = Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions
        {
            AllowAnonymous = true
        });

        var tokenSettings = Options.Create(new LinkTokenServiceSettings
        {
            SigningKey = "test"
        });

        var tokenService = new Mock<ICreateSystemToken>();

        return new DataAcquisitionServiceClient(serviceRegistry, bearerOptions, tokenSettings, tokenService.Object);
    }

}
