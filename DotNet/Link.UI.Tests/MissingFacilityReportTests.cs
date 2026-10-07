using System.Reflection;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

public class MissingFacilityReportTests
{
    private const string FacilityId = "05a4cf42-8920-4935-88df-695787a9ce08";
    private static readonly Guid ReportId = Guid.Parse("6b79dec7-7de6-49c0-b0b4-c933e8ca2139");

    [Fact]
    public async Task Missing_facility_still_opens_the_schedule()
    {
        var page = await Load(scheduleFacilityId: FacilityId, scheduleStatus: 200);

        page.NotFound.Should().BeFalse();
        page.LoadError.Should().BeNull();
        page.FacilityMissing.Should().BeTrue();
        page.FacilityName.Should().BeNull();
        page.Report.Should().NotBeNull();
        page.Report!.Id.Should().Be(ReportId);
        page.Report.FacilityId.Should().Be(FacilityId);
        page.Report.Status.Should().Be(ScheduleStatus.Submitted);
        page.Report.CensusCount.Should().Be(4);
        page.Report.InitialPopulationCount.Should().Be(7);
        page.Patients.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_facility_and_missing_schedule_says_the_report_was_not_found()
    {
        var page = await Load(scheduleFacilityId: FacilityId, scheduleStatus: 404);

        page.NotFound.Should().BeTrue();
        page.FacilityMissing.Should().BeTrue();
        page.Report.Should().BeNull();
        page.LoadError.Should().Be("That report was not found.");
    }

    [Fact]
    public async Task Schedule_for_another_facility_stays_rejected()
    {
        var page = await Load(scheduleFacilityId: "other-facility", scheduleStatus: 200);

        page.NotFound.Should().BeTrue();
        page.Report.Should().BeNull();
        page.LoadError.Should().Be("That report is not for this facility.");
    }

    private static Task<ReportDetailModel> Load(string scheduleFacilityId, int scheduleStatus)
    {
        var facilities = CallStub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>
        {
            ["GetAsync"] = _ => Task.FromResult(new LinkApiResponse<LantanaGroup.Link.Shared.Application.Models.Tenant.FacilityModel>
            {
                StatusCode = StatusCodes.Status404NotFound
            })
        });
        var reports = CallStub.Create<IReportServiceClient>(new Dictionary<string, Func<object?[]?, object?>>
        {
            ["GetScheduleAsync"] = _ => Task.FromResult(new LinkApiResponse<ReportScheduleApiModel>
            {
                StatusCode = scheduleStatus,
                Body = scheduleStatus == 200
                    ? new ReportScheduleApiModel
                    {
                        Id = ReportId,
                        FacilityId = scheduleFacilityId,
                        Status = ScheduleStatus.Submitted
                    }
                    : null
            }),
            ["GetEntryCountByScheduleAsync"] = _ => Task.FromResult(new LinkApiResponse<int> { StatusCode = 200, Body = 4 }),
            ["GetInitialPopulationCountAsync"] = _ => Task.FromResult(new LinkApiResponse<int> { StatusCode = 200, Body = 7 }),
            ["SearchEntriesAsync"] = _ => Task.FromResult(new LinkApiResponse<PagedConfigModel<ReportEntryApiModel>>
            {
                StatusCode = 200,
                Body = new PagedConfigModel<ReportEntryApiModel>([], new PaginationMetadata(10, 1, 0))
            })
        });
        var service = new FacilityViewService(
            facilities,
            reports,
            acquisition: null,
            normalization: null,
            dmrp: null,
            admin: null,
            Options.Create(new LinkUiFeatureOptions()),
            NullLogger<FacilityViewService>.Instance);
        return service.LoadReportAsync(FacilityId, ReportId.ToString(), query: null, CancellationToken.None);
    }

    private class CallStub : DispatchProxy
    {
        public Dictionary<string, Func<object?[]?, object?>> Methods { get; set; } = new(StringComparer.Ordinal);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is not null && Methods.TryGetValue(targetMethod.Name, out var method))
                return method(args);
            throw new NotSupportedException(targetMethod?.Name);
        }

        public static T Create<T>(Dictionary<string, Func<object?[]?, object?>> methods) where T : class
        {
            var created = DispatchProxy.Create<T, CallStub>();
            ((CallStub)(object)created).Methods = methods;
            return created;
        }
    }
}
