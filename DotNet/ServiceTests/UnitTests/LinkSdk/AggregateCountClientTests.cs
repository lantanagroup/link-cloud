using System.Text.Json;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Audit;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;
using Moq;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class AggregateCountClientTests
{
    [Fact]
    public async System.Threading.Tasks.Task Report_posts_the_activity_count_body()
    {
        using var http = new FakeHttpBoundary("""{"inFlight":4,"submitted":1,"notSubmitted":0,"failed":0,"createdPerDay":[]}""");
        using var client = new ReportServiceClient(
            Options.Create(new ServiceRegistry { ReportServiceUrl = http.BaseUrl }),
            Bearer(),
            Token(),
            new Mock<ICreateSystemToken>().Object);

        var result = await client.GetActivityCountsAsync(new ReportActivityCountRequest
        {
            Days = 7,
            FacilityIds = ["facility-a"]
        });
        var request = http.SingleRequest();

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/schedules/counts", request.Path);
        Assert.Equal(7, JsonDocument.Parse(request.Body).RootElement.GetProperty("Days").GetInt32());
        Assert.Equal("facility-a", JsonDocument.Parse(request.Body).RootElement.GetProperty("FacilityIds")[0].GetString());
        Assert.Equal(4, result.Body!.InFlight);
    }

    [Fact]
    public async System.Threading.Tasks.Task Tenant_posts_the_facility_count_body()
    {
        using var http = new FakeHttpBoundary("""{"total":9,"matched":2}""");
        using var client = new FacilityServiceClient(
            Options.Create(new ServiceRegistry
            {
                TenantService = new TenantServiceRegistration { TenantServiceUrl = http.BaseUrl }
            }),
            Bearer(),
            Token(),
            new Mock<ICreateSystemToken>().Object);

        var result = await client.GetFacilityCountsAsync(new FacilityCountRequest { FacilityIds = ["f1"] });
        var request = http.SingleRequest();

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/Facility/counts", request.Path);
        Assert.Equal("f1", JsonDocument.Parse(request.Body).RootElement.GetProperty("FacilityIds")[0].GetString());
        Assert.Equal(9, result.Body!.Total);
        Assert.Equal(2, result.Body.Matched);
    }

    [Fact]
    public async System.Threading.Tasks.Task Acquisition_posts_the_day_count_body()
    {
        using var http = new FakeHttpBoundary("""{"failedTotal":6,"days":[]}""");
        using var client = new DataAcquisitionServiceClient(
            Options.Create(new ServiceRegistry { DataAcquisitionServiceUrl = http.BaseUrl }),
            Bearer(),
            Token(),
            new Mock<ICreateSystemToken>().Object);

        var result = await client.GetActivityCountsAsync(new AcquisitionActivityCountRequest { Days = 14 });
        var request = http.SingleRequest();

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/counts", request.Path);
        Assert.Equal(14, JsonDocument.Parse(request.Body).RootElement.GetProperty("Days").GetInt32());
        Assert.Equal(6, result.Body!.FailedTotal);
    }

    [Fact]
    public async System.Threading.Tasks.Task Audit_gets_the_error_count_for_the_window()
    {
        using var http = new FakeHttpBoundary("""{"hours":24,"errors":3}""");
        using var client = new AuditServiceClient(
            Options.Create(new ServiceRegistry { AuditServiceUrl = http.BaseUrl }),
            Bearer(),
            Token(),
            new Mock<ICreateSystemToken>().Object);

        var result = await client.GetErrorCountAsync(24);
        var request = http.SingleRequest();

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/audit/errors", request.Path);
        Assert.Contains("hours=24", request.Query);
        Assert.Equal(3, result.Body!.Errors);
    }

    private static IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> Bearer() =>
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true });

    private static IOptions<LinkTokenServiceSettings> Token() =>
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" });
}
