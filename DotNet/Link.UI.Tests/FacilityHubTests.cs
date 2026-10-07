using FluentAssertions;
using Microsoft.AspNetCore.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

public class FacilityFormRulesTests
{
    [Theory]
    [InlineData("ehr-test3", false, true)]
    [InlineData("ABC-12", false, true)]
    [InlineData("12345", true, true)]
    [InlineData("123456", true, false)]
    [InlineData("ehr test", false, false)]
    [InlineData("", false, false)]
    public void Facility_id_follows_the_tenant_rule(string facilityId, bool numericOnly, bool valid)
    {
        FacilityFormRules.IsValidFacilityId(facilityId, numericOnly).Should().Be(valid);
    }

    [Fact]
    public void Timezone_must_be_iana()
    {
        FacilityFormRules.IsIanaTimeZone("America/Chicago").Should().BeTrue();
        FacilityFormRules.IsIanaTimeZone("Eastern Standard Time").Should().BeFalse();
        FacilityFormRules.TimeZones(null).Should().Contain("America/Chicago");
    }

    [Fact]
    public void Dmrp_on_saves_an_empty_schedule_even_when_the_form_names_reports()
    {
        var built = FacilityFormRules.TryBuildFacility(
            new FacilityEditInput
            {
                FacilityId = "ehr-test3",
                FacilityName = "Test",
                TimeZone = "America/Chicago",
                DailyReports = "MeasureA",
                WeeklyReports = "MeasureA"
            },
            dmrpEnabled: true,
            numericOnlyFacilityId: false,
            vendorListLoaded: true,
            currentVendorVersionId: null,
            allowedVendorVersionIds: new HashSet<Guid>(),
            out var model,
            out var error);

        built.Should().BeTrue(error);
        model!.ScheduledReports.Daily.Should().BeEmpty();
        model.ScheduledReports.Weekly.Should().BeEmpty();
        model.ScheduledReports.Monthly.Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_reports_across_periods_are_rejected_when_dmrp_is_off()
    {
        var built = FacilityFormRules.TryBuildFacility(
            new FacilityEditInput
            {
                FacilityId = "ehr-test3",
                FacilityName = "Test",
                TimeZone = "America/Chicago",
                DailyReports = "MeasureA",
                MonthlyReports = "MeasureA"
            },
            dmrpEnabled: false,
            numericOnlyFacilityId: false,
            vendorListLoaded: true,
            currentVendorVersionId: null,
            allowedVendorVersionIds: new HashSet<Guid>(),
            out _,
            out var error);

        built.Should().BeFalse();
        error.Should().Contain("unique");
    }

    [Fact]
    public void Duration_uses_the_query_dispatch_parser()
    {
        FacilityFormRules.IsDuration("PT10S").Should().BeTrue();
        FacilityFormRules.IsDuration("PT1H30M").Should().BeTrue();
        FacilityFormRules.IsDuration("10s").Should().BeFalse();
    }

    [Fact]
    public void Blank_and_removed_schedule_rows_are_dropped()
    {
        var ok = FacilityFormRules.TryNormalizeSchedules(
            new[]
            {
                new DispatchScheduleInput { Event = "Discharge", Duration = "PT10S" },
                new DispatchScheduleInput { Event = "Admit", Duration = "PT5M", Remove = true },
                new DispatchScheduleInput()
            },
            out var schedules,
            out var error);

        ok.Should().BeTrue(error);
        schedules.Should().ContainSingle();
        schedules[0].Event.Should().Be("Discharge");
    }

    [Fact]
    public void A_half_filled_schedule_row_is_rejected()
    {
        var ok = FacilityFormRules.TryNormalizeSchedules(
            new[] { new DispatchScheduleInput { Event = "Discharge" } },
            out _,
            out var error);

        ok.Should().BeFalse();
        error.Should().Contain("duration");
    }

    [Fact]
    public void Unreachable_service_is_not_described_as_an_http_status()
    {
        FacilityFormRules.ServiceMessage("Census", 0, null).Should().Contain("could not be reached");
        FacilityFormRules.ServiceMessage("Tenant", 400, """{"title":"Bad Request","detail":"Timezone Not Found: Foo"}""")
            .Should().Contain("Timezone Not Found: Foo");
    }
}

public class FacilityHubServiceTests
{
    [Fact]
    public async Task Missing_facility_is_not_a_load_failure()
    {
        var facilities = new FakeFacilities
        {
            Facility = Response<FacilityModel>(StatusCodes.Status404NotFound, null)
        };
        var hub = Hub(facilities, new FakeCensus(), new FakeQueryDispatch(), dmrp: true);

        var page = await hub.LoadEditAsync("missing-facility", CancellationToken.None);

        page.NotFound.Should().BeTrue();
        page.LoadError.Should().BeNull();
        page.FacilityId.Should().Be("missing-facility");
    }

    [Fact]
    public async Task Census_404_means_there_is_no_configuration()
    {
        var facilities = Facility("hub-1");
        var census = new FakeCensus
        {
            Config = Response<CensusConfigApiModel>(StatusCodes.Status404NotFound, null)
        };
        var hub = Hub(facilities, census, new FakeQueryDispatch { Config = Response<QueryDispatchConfigurationApiModel>(StatusCodes.Status404NotFound, null) }, dmrp: true);

        var page = await hub.LoadEditAsync("hub-1", CancellationToken.None);

        page.CensusConfigured.Should().BeTrue();
        page.CensusExists.Should().BeFalse();
        page.CensusError.Should().BeNull();
        page.QueryDispatchExists.Should().BeFalse();
        page.Schedules.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_with_dmrp_sends_an_empty_schedule()
    {
        var facilities = new FakeFacilities
        {
            Vendors = Response(StatusCodes.Status200OK, new List<VendorVersionModel>()),
            Created = Response(StatusCodes.Status201Created, new FacilityModel { FacilityId = "hub-1" })
        };
        var hub = Hub(facilities, census: null, queryDispatch: null, dmrp: true);

        var result = await hub.CreateAsync(new FacilityEditInput
        {
            FacilityId = "hub-1",
            FacilityName = "Hub",
            TimeZone = "America/Chicago",
            DailyReports = "ShouldNotBeSent"
        }, CancellationToken.None);

        result.RedirectFacilityId.Should().Be("hub-1");
        facilities.CreatedBody!.ScheduledReports.Daily.Should().BeEmpty();
        facilities.CreatedBody.ScheduledReports.Weekly.Should().BeEmpty();
        facilities.CreatedBody.ScheduledReports.Monthly.Should().BeEmpty();
    }

    [Fact]
    public async Task Saving_census_creates_when_the_panel_says_it_is_missing()
    {
        var facilities = Facility("hub-1");
        var census = new FakeCensus
        {
            Config = Response<CensusConfigApiModel>(StatusCodes.Status404NotFound, null),
            Saved = Response(StatusCodes.Status201Created, new CensusConfigApiModel())
        };
        var hub = Hub(facilities, census, queryDispatch: null, dmrp: true);

        var result = await hub.SaveCensusAsync("hub-1", enabled: true, scheduledTrigger: "0 0 6 * * ?", censusExists: false, CancellationToken.None);

        result.RedirectFacilityId.Should().Be("hub-1");
        census.Created.Should().NotBeNull();
        census.Created!.ScheduledTrigger.Should().Be("0 0 6 * * ?");
        census.Updated.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_duration_does_not_call_query_dispatch()
    {
        var facilities = Facility("hub-1");
        var dispatch = new FakeQueryDispatch
        {
            Config = Response(StatusCodes.Status200OK, new QueryDispatchConfigurationApiModel
            {
                FacilityId = "hub-1",
                DispatchSchedules = [new DispatchScheduleApiModel { Event = "Discharge", Duration = "PT10S" }]
            })
        };
        var hub = Hub(facilities, census: null, dispatch, dmrp: true);

        var result = await hub.SaveQueryDispatchAsync(
            "hub-1",
            [new DispatchScheduleInput { Event = "Discharge", Duration = "nope" }],
            queryDispatchExists: true,
            CancellationToken.None);

        result.Page!.QueryDispatchError.Should().Contain("PT10S");
        dispatch.Upserted.Should().BeNull();
        dispatch.Created.Should().BeNull();
    }

    [Fact]
    public async Task Soft_delete_204_returns_to_the_list()
    {
        var facilities = Facility("hub-1");
        var hub = Hub(facilities, census: null, queryDispatch: null, dmrp: true);

        var result = await hub.SoftDeleteAsync("hub-1", CancellationToken.None);

        result.RedirectToList.Should().BeTrue();
        result.RedirectMessage.Should().Be("Facility removed.");
        facilities.SoftDeletedId.Should().Be("hub-1");
    }

    [Fact]
    public async Task Soft_delete_404_stays_on_a_not_found_page()
    {
        var facilities = Facility("hub-1");
        facilities.SoftDeleted = new LinkApiResponse { StatusCode = StatusCodes.Status404NotFound };
        var hub = Hub(facilities, census: null, queryDispatch: null, dmrp: true);

        var result = await hub.SoftDeleteAsync("hub-1", CancellationToken.None);

        result.RedirectToList.Should().BeFalse();
        result.Page!.NotFound.Should().BeTrue();
    }

    [Fact]
    public async Task A_missing_census_url_does_not_fail_the_facility_page()
    {
        var hub = Hub(Facility("hub-1"), census: null, queryDispatch: null, dmrp: true);

        var page = await hub.LoadEditAsync("hub-1", CancellationToken.None);

        page.LoadError.Should().BeNull();
        page.FacilityName.Should().Be("Hub");
        page.CensusConfigured.Should().BeFalse();
        page.QueryDispatchConfigured.Should().BeFalse();
        page.DataAcquisitionConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task A_missing_data_acquisition_url_does_not_save()
    {
        var hub = Hub(Facility("hub-1"), census: null, queryDispatch: null, dmrp: true);

        var result = await hub.SaveFhirQueryAsync("hub-1", new FhirQueryPanel(), CancellationToken.None);

        result.RedirectFacilityId.Should().BeNull();
        result.Page!.FhirQueryError.Should().Contain("DataAcquisitionServiceUrl");
    }

    [Fact]
    public async Task A_missing_normalization_url_does_not_save()
    {
        var hub = Hub(Facility("hub-1"), census: null, queryDispatch: null, dmrp: true);

        var result = await hub.SaveOperationAsync("hub-1", new NormalizationOperationInput
        {
            OperationType = "CopyProperty",
            Name = "Proof"
        }, CancellationToken.None);

        result.RedirectFacilityId.Should().BeNull();
        result.Page!.NormalizationError.Should().Contain("NormalizationServiceUrl");
    }

    private static FacilityHubService Hub(
        FakeFacilities facilities,
        FakeCensus? census,
        FakeQueryDispatch? queryDispatch,
        bool dmrp) =>
        new(
            facilities,
            census,
            queryDispatch,
            Options.Create(new LinkUiFeatureOptions { DmrpEnabled = dmrp }),
            NullLogger<FacilityHubService>.Instance);

    private static FakeFacilities Facility(string id) => new()
    {
        Facility = Response(StatusCodes.Status200OK, new FacilityModel
        {
            FacilityId = id,
            FacilityName = "Hub",
            TimeZone = "America/Chicago",
            ScheduledReports = FacilityFormRules.EmptySchedule()
        }),
        Vendors = Response(StatusCodes.Status200OK, new List<VendorVersionModel>())
    };

    private static LinkApiResponse<T> Response<T>(int status, T? body) => new()
    {
        StatusCode = status,
        Body = body
    };
}

sealed class FakeFacilities : IFacilityServiceClient
{
    public LinkApiResponse<FacilityModel> Facility { get; set; } = new() { StatusCode = 404 };
    public LinkApiResponse<List<VendorVersionModel>> Vendors { get; set; } = new() { StatusCode = 200, Body = new() };
    public LinkApiResponse<FacilityModel> Created { get; set; } = new() { StatusCode = 201 };
    public FacilityModel? CreatedBody { get; private set; }

    public Task<LinkApiResponse<FacilityModel>> GetAsync(string facilityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Facility);

    public Task<LinkApiResponse<List<VendorVersionModel>>> GetVendorVersionsAsync(Guid? vendorId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vendors);

    public Task<LinkApiResponse<FacilityModel>> CreateAsync(FacilityModel request, CancellationToken cancellationToken = default)
    {
        CreatedBody = request;
        return Task.FromResult(Created);
    }

    public Task<LinkApiResponse<FacilityModel>> UpdateAsync(string facilityId, FacilityModel request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public LinkApiResponse SoftDeleted { get; set; } = new() { StatusCode = 204 };
    public string? SoftDeletedId { get; private set; }

    public Task<LinkApiResponse> SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        SoftDeletedId = facilityId;
        return Task.FromResult(SoftDeleted);
    }

    public Task<LinkApiResponse<VendorModel>> CreateVendorAsync(CreateVendorModel request, CancellationToken cancellationToken = default) => Unused<VendorModel>();
    public Task<LinkApiResponse<VendorModel>> GetVendorAsync(Guid vendorId, CancellationToken cancellationToken = default) => Unused<VendorModel>();
    public Task<LinkApiResponse<List<VendorModel>>> GetVendorsAsync(CancellationToken cancellationToken = default) => Unused<List<VendorModel>>();
    public Task<LinkApiResponse<VendorModel>> UpdateVendorAsync(Guid vendorId, UpdateVendorModel request, CancellationToken cancellationToken = default) => Unused<VendorModel>();
    public Task<LinkApiResponse> DeleteVendorAsync(Guid vendorId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse<VendorVersionModel>> CreateVendorVersionAsync(CreateVendorVersionModel request, CancellationToken cancellationToken = default) => Unused<VendorVersionModel>();
    public Task<LinkApiResponse<VendorVersionModel>> GetVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default) => Unused<VendorVersionModel>();
    public Task<LinkApiResponse<VendorVersionModel>> UpdateVendorVersionAsync(Guid vendorVersionId, UpdateVendorVersionModel request, CancellationToken cancellationToken = default) => Unused<VendorVersionModel>();
    public Task<LinkApiResponse> DeleteVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> CheckFacilityExistsAsync(string facilityId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> DeleteAsync(string facilityId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> RestoreAsync(string facilityId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> SearchFacilitiesAsync(string? facilityId = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse<Dictionary<string, string>>> GetFacilityListAsync(string? search = null, bool includeDeleted = false, CancellationToken cancellationToken = default) => Unused<Dictionary<string, string>>();
    public Task<LinkApiResponse<GenerateAdhocReportResponseApiModel>> GenerateAdhocReportAsync(string facilityId, AdHocReportRequest request, CancellationToken cancellationToken = default) => Unused<GenerateAdhocReportResponseApiModel>();
    public Task<LinkApiResponse<GenerateAdhocReportResponseApiModel>> RegenerateReportAsync(string facilityId, RegenerateReportRequest request, CancellationToken cancellationToken = default) => Unused<GenerateAdhocReportResponseApiModel>();

    private static Task<LinkApiResponse<T>> Unused<T>() => throw new NotSupportedException();
    private static Task<LinkApiResponse> Unused() => throw new NotSupportedException();
}

sealed class FakeCensus : ICensusServiceClient
{
    public LinkApiResponse<CensusConfigApiModel> Config { get; set; } = new() { StatusCode = 404 };
    public LinkApiResponse<CensusConfigApiModel> Saved { get; set; } = new() { StatusCode = 200 };
    public CensusConfigApiModel? Created { get; private set; }
    public CensusConfigApiModel? Updated { get; private set; }

    public Task<LinkApiResponse<CensusConfigApiModel>> GetCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Config);

    public Task<LinkApiResponse<CensusConfigApiModel>> CreateCensusConfigAsync(CensusConfigApiModel request, CancellationToken cancellationToken = default)
    {
        Created = request;
        return Task.FromResult(Saved);
    }

    public Task<LinkApiResponse<CensusConfigApiModel>> UpdateCensusConfigAsync(string facilityId, CensusConfigApiModel request, CancellationToken cancellationToken = default)
    {
        Updated = request;
        return Task.FromResult(Saved);
    }

    public Task<LinkApiResponse> DeleteCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LinkApiResponse { StatusCode = 204 });

    public Task<LinkApiResponse> DisableFacilityJobsAsync(string facilityId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> EnableFacilityJobsAsync(string facilityId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> GetAdmittedPatientsAsync(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> GetCurrentPatientEncountersAsync(string facilityId, string? correlationId = null, string? sortBy = null, LantanaGroup.Link.Shared.Application.Enums.SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> GetCurrentPatientEncountersAsync(string facilityId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> GetHistoricalPatientEncountersAsync(string facilityId, DateTime? dateThreshold, string? correlationId = null, string? sortBy = null, LantanaGroup.Link.Shared.Application.Enums.SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> GetHistoricalPatientEncountersAsync(string facilityId, DateTime? dateThreshold, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> RebuildPatientEncountersAsync(string facilityId, string? correlationId = null, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> GetPatientEventsAsync(string facilityId, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> DeletePatientEventAsync(string id, CancellationToken cancellationToken = default) => Unused();
    public Task<LinkApiResponse> DeletePatientEventsByCorrelationAsync(string correlationId, CancellationToken cancellationToken = default) => Unused();

    private static Task<LinkApiResponse> Unused() => throw new NotSupportedException();
}

sealed class FakeQueryDispatch : IQueryDispatchServiceClient
{
    public LinkApiResponse<QueryDispatchConfigurationApiModel> Config { get; set; } = new() { StatusCode = 404 };
    public QueryDispatchConfigurationApiModel? Created { get; private set; }
    public QueryDispatchConfigurationApiModel? Upserted { get; private set; }

    public Task<LinkApiResponse<QueryDispatchConfigurationApiModel>> GetConfigurationAsync(string facilityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Config);

    public Task<LinkApiResponse> CreateQueryDispatchConfigurationAsync(QueryDispatchConfigurationApiModel configuration, CancellationToken cancellationToken = default)
    {
        Created = configuration;
        return Task.FromResult(new LinkApiResponse { StatusCode = 201 });
    }

    public Task<LinkApiResponse> UpsertQueryDispatchConfigurationAsync(string facilityId, QueryDispatchConfigurationApiModel configuration, CancellationToken cancellationToken = default)
    {
        Upserted = configuration;
        return Task.FromResult(new LinkApiResponse { StatusCode = 200 });
    }

    public Task<LinkApiResponse> DeleteQueryDispatchConfigurationAsync(string facilityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LinkApiResponse { StatusCode = 204 });
}
