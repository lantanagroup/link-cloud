using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

/// <summary>
/// Section writes shared by the facility pages and the run engine.
/// Run ensure defaults and DMRP idempotency stay covered by <see cref="FacilitySetupHelperTests"/>,
/// which calls the same service through the run-engine entry.
/// </summary>
[Trait("Category", "UnitTests")]
public class FacilityConfigurationServiceTests
{
    private readonly Mock<IAutomationOutput> _output = new();

    public FacilityConfigurationServiceTests()
    {
        _output.Setup(o => o.WriteLine(It.IsAny<string>()));
    }

    [Fact]
    public async Task Page_create_posts_the_callers_facility_and_does_not_apply_run_defaults()
    {
        FacilityModel? posted = null;
        var facilities = new Mock<IFacilityServiceClient>(MockBehavior.Strict);
        facilities.Setup(f => f.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .Callback<FacilityModel, CancellationToken>((model, _) => posted = model)
            .ReturnsAsync(new LinkApiResponse<FacilityModel> { StatusCode = 201 });

        var result = await FacilityConfigurationService.CreateFacilityAsync(facilities.Object, new FacilityModel
        {
            FacilityId = "unify-page",
            FacilityName = "Unify Page",
            TimeZone = "America/New_York",
            ScheduledReports = new TenantScheduledReportConfig { Daily = ["DailyMeasure"], Weekly = [], Monthly = [] }
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(posted);
        Assert.Equal("Unify Page", posted!.FacilityName);
        Assert.Equal("America/New_York", posted.TimeZone);
        Assert.Equal(["DailyMeasure"], posted.ScheduledReports.Daily);
        Assert.Null(posted.Vendor);
        facilities.Verify(f => f.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_create_returns_the_tenant_failure_without_throwing()
    {
        var facilities = new Mock<IFacilityServiceClient>(MockBehavior.Strict);
        facilities.Setup(f => f.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<FacilityModel> { StatusCode = 409, RawBody = "already exists" });

        var result = await FacilityConfigurationService.CreateFacilityAsync(
            facilities.Object,
            new FacilityModel { FacilityId = "unify-page", FacilityName = "Unify Page" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("already exists", result.RawBody);
    }

    [Fact]
    public async Task Page_census_save_creates_or_updates_the_callers_trigger()
    {
        var census = new Mock<ICensusServiceClient>(MockBehavior.Strict);
        CensusConfigApiModel? created = null;
        CensusConfigApiModel? updated = null;
        census.Setup(c => c.CreateCensusConfigAsync(It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<CensusConfigApiModel, CancellationToken>((model, _) => created = model)
            .ReturnsAsync(new LinkApiResponse<CensusConfigApiModel> { StatusCode = 201 });
        census.Setup(c => c.UpdateCensusConfigAsync(It.IsAny<string>(), It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<string, CensusConfigApiModel, CancellationToken>((_, model, _) => updated = model)
            .ReturnsAsync(new LinkApiResponse<CensusConfigApiModel> { StatusCode = 200 });

        var request = new CensusConfigApiModel
        {
            FacilityId = "page-1",
            ScheduledTrigger = "0 0 6 * * ?",
            Enabled = true
        };

        var createdResult = await FacilityConfigurationService.SaveCensusAsync(census.Object, request, exists: false, CancellationToken.None);
        var updatedResult = await FacilityConfigurationService.SaveCensusAsync(census.Object, request, exists: true, CancellationToken.None);

        Assert.True(createdResult.Success);
        Assert.True(updatedResult.Success);
        Assert.Equal("0 0 6 * * ?", created!.ScheduledTrigger);
        Assert.Equal("0 0 6 * * ?", updated!.ScheduledTrigger);
        census.Verify(c => c.CreateCensusConfigAsync(It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()), Times.Once);
        census.Verify(c => c.UpdateCensusConfigAsync("page-1", It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_census_ensure_skips_an_unchanged_config_and_creates_when_missing()
    {
        var census = new Mock<ICensusServiceClient>(MockBehavior.Strict);
        census.Setup(c => c.GetCensusConfigAsync("kept", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<CensusConfigApiModel>
            {
                StatusCode = 200,
                Body = new CensusConfigApiModel { FacilityId = "kept", ScheduledTrigger = "0 0/5 * * * ?", Enabled = false }
            });
        census.Setup(c => c.GetCensusConfigAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<CensusConfigApiModel> { StatusCode = 404 });
        CensusConfigApiModel? created = null;
        census.Setup(c => c.CreateCensusConfigAsync(It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<CensusConfigApiModel, CancellationToken>((model, _) => created = model)
            .ReturnsAsync(new LinkApiResponse<CensusConfigApiModel> { StatusCode = 201 });

        await FacilityConfigurationService.EnsureCensusConfigAsync(census.Object, _output.Object, "kept", enabled: false);
        await FacilityConfigurationService.EnsureCensusConfigAsync(census.Object, _output.Object, "missing", enabled: false);

        Assert.Equal("missing", created!.FacilityId);
        Assert.Equal(false, created.Enabled);
        Assert.Equal("0 0/5 * * * ?", created.ScheduledTrigger);
        census.Verify(c => c.UpdateCensusConfigAsync(It.IsAny<string>(), It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()), Times.Never);
        census.Verify(c => c.CreateCensusConfigAsync(It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_census_ensure_throws_when_create_fails()
    {
        var census = new Mock<ICensusServiceClient>(MockBehavior.Strict);
        census.Setup(c => c.GetCensusConfigAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<CensusConfigApiModel> { StatusCode = 404 });
        census.Setup(c => c.CreateCensusConfigAsync(It.IsAny<CensusConfigApiModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<CensusConfigApiModel> { StatusCode = 503, RawBody = "census down" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FacilityConfigurationService.EnsureCensusConfigAsync(census.Object, _output.Object, "missing", enabled: false));

        Assert.Contains("Failed to create census config", exception.Message);
        Assert.Contains("503", exception.Message);
    }

    [Fact]
    public async Task Page_query_dispatch_creates_or_upserts_the_callers_schedules()
    {
        var dispatch = new Mock<IQueryDispatchServiceClient>(MockBehavior.Strict);
        QueryDispatchConfigurationApiModel? created = null;
        QueryDispatchConfigurationApiModel? upserted = null;
        dispatch.Setup(d => d.CreateQueryDispatchConfigurationAsync(It.IsAny<QueryDispatchConfigurationApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<QueryDispatchConfigurationApiModel, CancellationToken>((model, _) => created = model)
            .ReturnsAsync(new LinkApiResponse { StatusCode = 201 });
        dispatch.Setup(d => d.UpsertQueryDispatchConfigurationAsync(It.IsAny<string>(), It.IsAny<QueryDispatchConfigurationApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<string, QueryDispatchConfigurationApiModel, CancellationToken>((_, model, _) => upserted = model)
            .ReturnsAsync(new LinkApiResponse { StatusCode = 200 });

        var request = new QueryDispatchConfigurationApiModel
        {
            FacilityId = "page-1",
            DispatchSchedules = [new DispatchScheduleApiModel { Event = "Admit", Duration = "PT1H" }]
        };

        Assert.True((await FacilityConfigurationService.SaveQueryDispatchAsync(dispatch.Object, request, exists: false, CancellationToken.None)).Success);
        Assert.True((await FacilityConfigurationService.SaveQueryDispatchAsync(dispatch.Object, request, exists: true, CancellationToken.None)).Success);
        Assert.Equal("Admit", created!.DispatchSchedules[0].Event);
        Assert.Equal("PT1H", upserted!.DispatchSchedules[0].Duration);
        dispatch.Verify(d => d.CreateQueryDispatchConfigurationAsync(It.IsAny<QueryDispatchConfigurationApiModel>(), It.IsAny<CancellationToken>()), Times.Once);
        dispatch.Verify(d => d.UpsertQueryDispatchConfigurationAsync("page-1", It.IsAny<QueryDispatchConfigurationApiModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_query_config_treats_a_conflict_as_already_present_and_clamps_concurrency()
    {
        CreateFhirQueryConfigurationRequestApiModel? posted = null;
        var dataAcquisition = new Mock<IDataAcquisitionServiceClient>(MockBehavior.Strict);
        dataAcquisition.Setup(d => d.CreateFhirQueryConfigurationAsync(It.IsAny<CreateFhirQueryConfigurationRequestApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<CreateFhirQueryConfigurationRequestApiModel, CancellationToken>((model, _) => posted = model)
            .ReturnsAsync(new LinkApiResponse { StatusCode = 409, RawBody = "exists" });

        var config = new AutomationConfig
        {
            FacilityFhirServerBase = "http://localhost/fhir",
            FhirQuery = new AutomationConfig.FhirQuerySettings { MaxConcurrentRequests = 8 }
        };

        await FacilityConfigurationService.EnsureQueryConfigAsync(dataAcquisition.Object, config, _output.Object, "run-1", concurrencyOverride: 16);

        Assert.NotNull(posted);
        Assert.Equal(8, posted!.MaxConcurrentRequests);
        Assert.Equal(3, posted.MaxRetries);
        Assert.Equal("http://localhost/fhir", posted.FhirServerBaseUrl);
        dataAcquisition.Verify(d => d.UpdateFhirQueryConfigurationAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_query_plan_create_does_not_delete_the_existing_plan()
    {
        var dataAcquisition = new Mock<IDataAcquisitionServiceClient>(MockBehavior.Strict);
        dataAcquisition.Setup(d => d.CreateQueryPlanAsync("page-1", It.IsAny<CreateQueryPlanRequestApiModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse { StatusCode = 201 });

        var result = await FacilityConfigurationService.CreateQueryPlanAsync(
            dataAcquisition.Object,
            "page-1",
            new CreateQueryPlanRequestApiModel { FacilityId = "page-1", Type = "Discharge", PlanName = "Custom" },
            CancellationToken.None);

        Assert.True(result.Success);
        dataAcquisition.Verify(d => d.DeleteQueryPlanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_notification_save_creates_or_updates_the_callers_model()
    {
        var notification = new Mock<INotificationServiceClient>(MockBehavior.Strict);
        NotificationConfigurationApiModel? created = null;
        NotificationConfigurationApiModel? updated = null;
        notification.Setup(n => n.CreateConfigurationAsync(It.IsAny<NotificationConfigurationApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationConfigurationApiModel, CancellationToken>((model, _) => created = model)
            .ReturnsAsync(new LinkApiResponse { StatusCode = 201 });
        notification.Setup(n => n.UpdateConfigurationAsync(It.IsAny<NotificationConfigurationApiModel>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationConfigurationApiModel, CancellationToken>((model, _) => updated = model)
            .ReturnsAsync(new LinkApiResponse { StatusCode = 200 });

        var model = new NotificationConfigurationApiModel { FacilityId = "page-1", EmailAddresses = ["ops@example.com"] };

        Assert.True((await FacilityConfigurationService.SaveNotificationConfigurationAsync(notification.Object, model, creating: true, CancellationToken.None)).Success);
        Assert.True((await FacilityConfigurationService.SaveNotificationConfigurationAsync(notification.Object, model, creating: false, CancellationToken.None)).Success);
        Assert.Equal("page-1", created!.FacilityId);
        Assert.Equal("ops@example.com", updated!.EmailAddresses![0]);
        notification.Verify(n => n.CreateConfigurationAsync(It.IsAny<NotificationConfigurationApiModel>(), It.IsAny<CancellationToken>()), Times.Once);
        notification.Verify(n => n.UpdateConfigurationAsync(It.IsAny<NotificationConfigurationApiModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
