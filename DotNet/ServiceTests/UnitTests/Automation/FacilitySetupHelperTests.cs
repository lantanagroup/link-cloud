using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

/// <summary>
/// The automation stack has to produce the same facility whether or not the Tenant service is
/// hosting the DMRP module, because everything downstream of it - the run, and the tenant database
/// validator - reads the same monthly schedule either way. What differs is how that schedule is set:
/// posted with the facility, or derived by Tenant from reporting plans this seeds first.
/// </summary>
[Trait("Category", "UnitTests")]
public class FacilitySetupHelperTests
{
    private const string FacilityId = "facility-under-test";
    private const string MeasureId = "NHSNAcuteCareHospitalMonthlyInitialPopulation";
    private const string MappingId = "mapping-id";

    private readonly Mock<IFacilityServiceClient> _facilityClient = new(MockBehavior.Strict);
    private readonly Mock<IDmrpServiceClient> _dmrpClient = new(MockBehavior.Strict);
    private readonly Mock<IAutomationOutput> _output = new();

    private readonly List<FacilityModel> _created = [];
    private readonly List<FacilityModel> _updated = [];

    public FacilitySetupHelperTests()
    {
        _output.Setup(o => o.WriteLine(It.IsAny<string>()));

        // The facility does not exist until it is created, and reads back once it does.
        var exists = false;

        _facilityClient.Setup(f => f.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => exists
                ? Response(200, new FacilityModel { FacilityId = FacilityId })
                : Response<FacilityModel>(404));

        _facilityClient.Setup(f => f.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityModel model, CancellationToken _) =>
            {
                _created.Add(model);
                exists = true;
                return Response(201, model);
            });

        _facilityClient.Setup(f => f.UpdateAsync(FacilityId, It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, FacilityModel model, CancellationToken _) =>
            {
                _updated.Add(model);
                return Response(200, model);
            });
    }

    [Fact]
    public async Task Posts_the_schedule_with_the_facility_when_dmrp_is_not_enabled()
    {
        GivenDmrpIsDisabled();

        await EnsureFacilityAsync();

        var created = Assert.Single(_created);
        Assert.Equal([MeasureId], created.ScheduledReports.Monthly);
        Assert.Empty(_updated);
    }

    [Fact]
    public async Task Leaves_the_dmrp_module_alone_when_it_is_not_enabled()
    {
        GivenDmrpIsDisabled();

        await EnsureFacilityAsync();

        // Strict mocks: any DMRP call beyond the probe would have thrown. Assert it explicitly so the
        // reason this matters survives a future loosening of the mock.
        _dmrpClient.Verify(d => d.CreateMeasureMappingAsync(It.IsAny<MeasureMappingModel>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _dmrpClient.Verify(d => d.CreateFacilityReportingPlanAsync(It.IsAny<FacilityReportingPlanRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The schedule is derived when the facility is saved, so it cannot be right on the first save:
    /// reporting plans need a facility to belong to. The second save is what makes Tenant derive it
    /// again, now that there is something to derive it from.
    /// </summary>
    [Fact]
    public async Task Creates_the_facility_with_no_schedule_then_saves_it_again_when_dmrp_is_enabled()
    {
        GivenDmrpIsEnabled();

        await EnsureFacilityAsync();

        var created = Assert.Single(_created);
        Assert.Empty(created.ScheduledReports.Monthly);

        var updated = Assert.Single(_updated);
        Assert.Empty(updated.ScheduledReports.Monthly);
        Assert.Empty(updated.ScheduledReports.Daily);
        Assert.Empty(updated.ScheduledReports.Weekly);
    }

    /// <summary>
    /// The run drives the pipeline with the measure's own id, so the mapping's dQM has to be that id
    /// for the derived schedule to name what the report types name.
    /// </summary>
    [Fact]
    public async Task Maps_the_measure_to_itself_monthly_when_dmrp_is_enabled()
    {
        GivenDmrpIsEnabled();

        MeasureMappingModel? mapping = null;
        _dmrpClient.Setup(d => d.CreateMeasureMappingAsync(It.IsAny<MeasureMappingModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeasureMappingModel model, CancellationToken _) =>
            {
                mapping = model;
                model.Id = MappingId;
                return Response(201, model);
            });

        await EnsureFacilityAsync();

        Assert.NotNull(mapping);
        Assert.Equal(MeasureId, mapping!.Measure);
        Assert.Equal(MeasureId, mapping.DQM);
        Assert.Equal(Frequency.Monthly, mapping.Frequency);
    }

    /// <summary>
    /// Mappings are shared by every run against a stack, so the second run must reuse the first one's
    /// rather than fail on the duplicate its creation would be.
    /// </summary>
    [Fact]
    public async Task Reuses_a_measure_mapping_that_already_exists()
    {
        GivenDmrpIsEnabled();

        _dmrpClient.Setup(d => d.SearchMeasureMappingsAsync(MeasureId, MeasureId, It.IsAny<Frequency?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(200, new PagedConfigModel<MeasureMappingModel>
            {
                Records = [new MeasureMappingModel { Id = MappingId, Measure = MeasureId, DQM = MeasureId }]
            }));

        var plans = CaptureReportingPlans();

        await EnsureFacilityAsync();

        _dmrpClient.Verify(d => d.CreateMeasureMappingAsync(It.IsAny<MeasureMappingModel>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.All(plans, plan => Assert.Equal(MappingId, plan.MeasureMappingId));
    }

    /// <summary>
    /// Enrollment is recorded per reporting period, and Tenant derives the schedule for the period the
    /// facility is in when it is saved. Seeding the next period as well is what keeps a run that
    /// crosses midnight on the first of a month from deriving an empty schedule.
    /// </summary>
    [Fact]
    public async Task Enrolls_the_facility_for_the_current_and_following_reporting_periods()
    {
        GivenDmrpIsEnabled();
        var plans = CaptureReportingPlans();

        await EnsureFacilityAsync();

        Assert.Equal(2, plans.Count);
        Assert.All(plans, plan =>
        {
            Assert.Equal(FacilityId, plan.FacilityId);
            Assert.True(plan.IsReporting);
        });

        var periods = plans.Select(p => new DateTime(p.ReportingYear, p.ReportingMonth, 1)).ToList();
        Assert.Equal(periods[0].AddMonths(1), periods[1]);
    }

    /// <summary>
    /// A reporting plan the facility already has is not an error; a run that reaches this point twice
    /// wants the enrollment, not a fresh row.
    /// </summary>
    [Fact]
    public async Task Accepts_a_reporting_plan_that_already_exists()
    {
        GivenDmrpIsEnabled();

        _dmrpClient.Setup(d => d.CreateFacilityReportingPlanAsync(It.IsAny<FacilityReportingPlanRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<FacilityReportingPlanModel>(409));

        await EnsureFacilityAsync();

        Assert.Single(_updated);
    }

    /// <summary>
    /// Guessing either way strands the run, so an answer that is neither "enabled" nor "disabled" has
    /// to stop it here, where the message can still name the request that failed.
    /// </summary>
    [Fact]
    public async Task Refuses_to_guess_when_the_dmrp_probe_fails()
    {
        _dmrpClient.Setup(d => d.SearchFacilityReportingPlansAsync(It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<PagedConfigModel<FacilityReportingPlanModel>>(500));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(EnsureFacilityAsync);

        Assert.Contains("api/dmrp/reporting-plans", exception.Message);
        Assert.Empty(_created);
    }

    /// <summary>
    /// A facility left behind by an earlier run is taken as it is. Enrolling it again would be the
    /// only way this could rewrite an existing facility's schedule, so it does not get that far.
    /// </summary>
    [Fact]
    public async Task Leaves_an_existing_facility_untouched()
    {
        _facilityClient.Setup(f => f.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(200, new FacilityModel { FacilityId = FacilityId }));

        await EnsureFacilityAsync();

        Assert.Empty(_created);
        Assert.Empty(_updated);
        _dmrpClient.VerifyNoOtherCalls();
    }

    private Task EnsureFacilityAsync() =>
        FacilitySetupHelper.EnsureFacilityAsync(_facilityClient.Object, _dmrpClient.Object, _output.Object,
            FacilityId, [MeasureId]);

    private void GivenDmrpIsDisabled() =>
        _dmrpClient.Setup(d => d.SearchFacilityReportingPlansAsync(It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<PagedConfigModel<FacilityReportingPlanModel>>(404));

    /// <summary>
    /// The probe answers, no mapping matches yet, and both writes succeed. Individual tests override
    /// whichever of these they are about.
    /// </summary>
    private void GivenDmrpIsEnabled()
    {
        _dmrpClient.Setup(d => d.SearchFacilityReportingPlansAsync(It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(200, new PagedConfigModel<FacilityReportingPlanModel>()));

        // Nothing matching answers 204 with no body rather than an empty page.
        _dmrpClient.Setup(d => d.SearchMeasureMappingsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Frequency?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<PagedConfigModel<MeasureMappingModel>>(204));

        _dmrpClient.Setup(d => d.CreateMeasureMappingAsync(It.IsAny<MeasureMappingModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeasureMappingModel model, CancellationToken _) =>
            {
                model.Id = MappingId;
                return Response(201, model);
            });

        _dmrpClient.Setup(d => d.CreateFacilityReportingPlanAsync(It.IsAny<FacilityReportingPlanRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityReportingPlanRequest request, CancellationToken _) =>
                Response(201, new FacilityReportingPlanModel { FacilityId = request.FacilityId }));
    }

    private List<FacilityReportingPlanRequest> CaptureReportingPlans()
    {
        var plans = new List<FacilityReportingPlanRequest>();

        _dmrpClient.Setup(d => d.CreateFacilityReportingPlanAsync(It.IsAny<FacilityReportingPlanRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityReportingPlanRequest request, CancellationToken _) =>
            {
                plans.Add(request);
                return Response(201, new FacilityReportingPlanModel { FacilityId = request.FacilityId });
            });

        return plans;
    }

    private static LinkApiResponse<T> Response<T>(int statusCode, T? body = default) =>
        new() { StatusCode = statusCode, Body = body };
}
