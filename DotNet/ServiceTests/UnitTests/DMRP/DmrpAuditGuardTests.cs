using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP;

/// <summary>
/// LEGLINK-1343 added five audit guards to the two DMRP controllers, and this covers each of their
/// branches on its own.
/// </summary>
/// <remarks>
/// <para>
/// The guards exist because neither condition is guaranteed. <c>Audit</c> resolves through
/// <c>HttpContext?.RequestServices?.GetService&lt;IAuditContext&gt;()</c>, so it is null whenever the
/// controller is used outside an audited request - which is how every other DMRP controller test
/// builds it. On the two deletes the row is read before it is removed, so that the note can name a
/// mapping that will not exist by the time the filter publishes; that read returns null for an id
/// nobody holds.
/// </para>
/// <para>
/// What each test asserts is that the guard changes nothing but the audit context: the response the
/// endpoint already returned on that path is still the response. The populated branches are covered
/// end to end in <c>IntegrationTests.DMRP.DmrpAuditTests</c>, which drives the real filter; here they
/// are asserted directly against the context so that each branch has a test of its own.
/// </para>
/// <para>
/// These are deliberately isolated: every collaborator is a Moq double, so nothing here touches a
/// database, Kafka or MeasureEval.
/// </para>
/// </remarks>
public class DmrpAuditGuardTests
{
    private const string Measure = "ACH";
    private const string Dqm = "NHSNAcuteCareHospitalDailyInitialPopulation";
    private const string FacilityId = "244bb2b5-9cfc-4613-9adf-3557de265559";

    private static readonly string RowId = Guid.NewGuid().ToString();

    // ---- measure mappings: delete ----

    /// <summary>
    /// The left side of the guard. An id nobody holds reads back nothing, so there is no mapping to
    /// name - and the delete still answers 404, which is what stops the filter publishing at all.
    /// </summary>
    [Fact]
    public async Task DeleteMeasureMapping_WhenTheMappingIsMissing_ReturnsNotFoundAndNamesNoValues()
    {
        var audit = new AuditContext();
        var queries = new Mock<IMeasureMappingQueries>();
        var manager = new Mock<IMeasureMappingManager>();

        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeasureMappingModel?)null);
        manager
            .Setup(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationException("No measure mapping has that Id."));

        var result = await MappingController(manager, queries, audit)
            .DeleteMeasureMapping(RowId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<NotFoundObjectResult>(result).StatusCode);
        Assert.Empty(audit.Values);
    }

    /// <summary>
    /// The right side of the guard. Outside an audited request there is nothing to write to, and the
    /// delete must still succeed rather than fault on a null context.
    /// </summary>
    [Fact]
    public async Task DeleteMeasureMapping_WithNoAuditContext_StillReturnsNoContent()
    {
        var queries = new Mock<IMeasureMappingQueries>();
        var manager = new Mock<IMeasureMappingManager>();

        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMappingModel());

        var result = await MappingController(manager, queries, audit: null)
            .DeleteMeasureMapping(RowId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        manager.Verify(m => m.DeleteAsync(RowId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Both sides true: the note's three values are filled in and the delete still answers 204.</summary>
    [Fact]
    public async Task DeleteMeasureMapping_WithAnExistingRowAndAnAuditContext_NamesTheMeasureDqmAndFrequency()
    {
        var audit = new AuditContext();
        var queries = new Mock<IMeasureMappingQueries>();
        var manager = new Mock<IMeasureMappingManager>();

        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMappingModel());

        var result = await MappingController(manager, queries, audit)
            .DeleteMeasureMapping(RowId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(Measure, audit.Values["Measure"]);
        Assert.Equal(Dqm, audit.Values["dqm"]);
        Assert.Equal(Frequency.Daily, audit.Values["Frequency"]);
    }

    // ---- measure mappings: create ----

    /// <summary>
    /// A create has no id in the route, so the handler is the only thing that can supply one. Without
    /// a context there is nowhere to put it, and the 201 must be unaffected.
    /// </summary>
    [Fact]
    public async Task CreateMeasureMapping_WithNoAuditContext_StillReturnsCreated()
    {
        var manager = new Mock<IMeasureMappingManager>();
        manager
            .Setup(m => m.CreateAsync(It.IsAny<MeasureMapping>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMappingEntity());

        var result = await MappingController(manager, new Mock<IMeasureMappingQueries>(), audit: null)
            .CreateMeasureMapping(ACreateRequest(), CancellationToken.None);

        Assert.Equal(RowId, Assert.IsType<MeasureMappingModel>(Assert.IsType<CreatedResult>(result).Value).Id);
    }

    /// <summary>With a context the new row's own id is recorded, which is what ResourceId must be.</summary>
    [Fact]
    public async Task CreateMeasureMapping_WithAnAuditContext_RecordsTheNewRowsId()
    {
        var audit = new AuditContext();
        var manager = new Mock<IMeasureMappingManager>();
        manager
            .Setup(m => m.CreateAsync(It.IsAny<MeasureMapping>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMappingEntity());

        var result = await MappingController(manager, new Mock<IMeasureMappingQueries>(), audit)
            .CreateMeasureMapping(ACreateRequest(), CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        Assert.Equal(RowId, audit.RecordId);
    }

    // ---- reporting plans: delete ----

    /// <summary>
    /// The plan equivalent of the missing-row branch. The manager reports the absence by throwing, so
    /// the 404 arrives from the catch rather than from the guard - the guard only keeps the note empty.
    /// </summary>
    [Fact]
    public async Task DeleteFacilityReportingPlan_WhenThePlanIsMissing_ReturnsNotFoundAndNamesNoValues()
    {
        var audit = new AuditContext();
        var queries = new Mock<IFacilityReportingPlanQueries>();
        var manager = new Mock<IFacilityReportingPlanManager>();

        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityReportingPlanModel?)null);
        manager
            .Setup(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("No reporting plan has that Id."));

        var result = await PlanController(manager, queries, audit)
            .DeleteFacilityReportingPlan(RowId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(audit.Values);
        Assert.Null(audit.FacilityId);
    }

    /// <summary>Outside an audited request the plan delete still answers 204.</summary>
    [Fact]
    public async Task DeleteFacilityReportingPlan_WithNoAuditContext_StillReturnsNoContent()
    {
        var queries = new Mock<IFacilityReportingPlanQueries>();
        var manager = new Mock<IFacilityReportingPlanManager>();

        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(APlanModel());

        var result = await PlanController(manager, queries, audit: null)
            .DeleteFacilityReportingPlan(RowId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        manager.Verify(m => m.DeleteAsync(RowId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Both sides true. A plan belongs to a facility that the delete route does not carry, so the
    /// handler supplies it alongside the note's values.
    /// </summary>
    [Fact]
    public async Task DeleteFacilityReportingPlan_WithAnExistingRowAndAnAuditContext_NamesTheFacilityAndPeriod()
    {
        var audit = new AuditContext();
        var queries = new Mock<IFacilityReportingPlanQueries>();
        var manager = new Mock<IFacilityReportingPlanManager>();

        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(APlanModel());

        var result = await PlanController(manager, queries, audit)
            .DeleteFacilityReportingPlan(RowId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(FacilityId, audit.FacilityId);
        Assert.Equal(Measure, audit.Values["Measure"]);
        Assert.Equal(7, audit.Values["month"]);
        Assert.Equal(2099, audit.Values["year"]);
    }

    // ---- reporting plans: create and update ----

    /// <summary>The create's guard, with nothing to write to.</summary>
    [Fact]
    public async Task CreateFacilityReportingPlan_WithNoAuditContext_StillReturnsCreated()
    {
        var manager = new Mock<IFacilityReportingPlanManager>();
        manager
            .Setup(m => m.CreateAsync(It.IsAny<FacilityReportingPlan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(APlanEntity());

        var result = await PlanController(manager, new Mock<IFacilityReportingPlanQueries>(), audit: null)
            .CreateFacilityReportingPlan(APlanRequest(), CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
    }

    /// <summary>
    /// The facility arrives in the body on this route rather than the URL, so without the handler the
    /// event would name no facility at all.
    /// </summary>
    [Fact]
    public async Task CreateFacilityReportingPlan_WithAnAuditContext_RecordsTheFacilityAndTheNewRowsId()
    {
        var audit = new AuditContext();
        var manager = new Mock<IFacilityReportingPlanManager>();
        manager
            .Setup(m => m.CreateAsync(It.IsAny<FacilityReportingPlan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(APlanEntity());

        var result = await PlanController(manager, new Mock<IFacilityReportingPlanQueries>(), audit)
            .CreateFacilityReportingPlan(APlanRequest(), CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        Assert.Equal(FacilityId, audit.FacilityId);
        Assert.Equal(RowId, audit.RecordId);
    }

    /// <summary>The update's guard, with nothing to write to.</summary>
    [Fact]
    public async Task UpdateFacilityReportingPlan_WithNoAuditContext_StillReturnsAccepted()
    {
        var queries = new Mock<IFacilityReportingPlanQueries>();
        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(APlanModel());

        var result = await PlanController(new Mock<IFacilityReportingPlanManager>(), queries, audit: null)
            .UpdateFacilityReportingPlan(RowId, APlanUpdateRequest(), CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
    }

    /// <summary>
    /// The update reads the row back after writing it, and takes the facility from what it read rather
    /// than from the request.
    /// </summary>
    [Fact]
    public async Task UpdateFacilityReportingPlan_WithAnAuditContext_RecordsTheFacilityFromTheSavedRow()
    {
        var audit = new AuditContext();
        var queries = new Mock<IFacilityReportingPlanQueries>();
        queries
            .Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(APlanModel());

        var result = await PlanController(new Mock<IFacilityReportingPlanManager>(), queries, audit)
            .UpdateFacilityReportingPlan(RowId, APlanUpdateRequest(), CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal(FacilityId, audit.FacilityId);
    }

    // ---- fixture helpers ----

    /// <summary>
    /// A controller context that either resolves an audit context or has no request services at all -
    /// the two states the guards distinguish. A bare <see cref="DefaultHttpContext"/> has null
    /// RequestServices, which is exactly how the controller is reached outside a request.
    /// </summary>
    private static ControllerContext ContextFor(IAuditContext? audit)
    {
        var httpContext = new DefaultHttpContext();

        if (audit is not null)
        {
            var services = new Mock<IServiceProvider>();
            services.Setup(sp => sp.GetService(typeof(IAuditContext))).Returns(audit);

            //ControllerBase resolves this with GetRequiredService, which throws against a provider that
            //does not have it. The plan controller's 404 goes through Problem(), so without this the
            //missing-row test would fault before reaching the assertion. A null RequestServices - the
            //no-audit-context case below - short-circuits that lookup instead, which is why the other
            //DMRP controller tests never need it.
            services.Setup(sp => sp.GetService(typeof(ProblemDetailsFactory)))
                .Returns(new StubProblemDetailsFactory());

            httpContext.RequestServices = services.Object;
        }

        return new ControllerContext { HttpContext = httpContext };
    }

    /// <summary>The smallest factory that lets <c>Problem()</c> produce a result to assert on.</summary>
    private sealed class StubProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null,
            string? title = null, string? type = null, string? detail = null, string? instance = null) =>
            new()
            {
                Status = statusCode,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };

        public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext,
            ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null,
            string? type = null, string? detail = null, string? instance = null) =>
            new(modelStateDictionary)
            {
                Status = statusCode,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
    }

    private static MeasureMappingsController MappingController(
        Mock<IMeasureMappingManager> manager,
        Mock<IMeasureMappingQueries> queries,
        IAuditContext? audit)
    {
        //the dQM check runs before the row is written, so it has to answer for a create to get as far
        //as the guard.
        var measureEval = new Mock<IMeasureEvalServiceClient>();
        measureEval
            .Setup(client => client.GetMeasureDefinitionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string> { StatusCode = StatusCodes.Status200OK });

        return new MeasureMappingsController(
            NullLogger<MeasureMappingsController>.Instance,
            manager.Object,
            queries.Object,
            measureEval.Object)
        {
            ControllerContext = ContextFor(audit)
        };
    }

    private static FacilityReportingPlansController PlanController(
        Mock<IFacilityReportingPlanManager> manager,
        Mock<IFacilityReportingPlanQueries> queries,
        IAuditContext? audit) =>
        new(
            NullLogger<FacilityReportingPlansController>.Instance,
            manager.Object,
            queries.Object,
            new Mock<IFacilityReportingPlanLookAhead>().Object,
            new Mock<IDmrpReportingPlanSync>().Object,
            new Mock<IFacilityExistence>().Object,
            new Mock<IFacilityReportingPeriodResolver>().Object)
        {
            ControllerContext = ContextFor(audit)
        };

    private static MeasureMappingModel AMappingModel() => new()
    {
        Id = RowId,
        Measure = Measure,
        DQM = Dqm,
        Frequency = Frequency.Daily
    };

    private static MeasureMapping AMappingEntity() => new()
    {
        Id = RowId,
        Measure = Measure,
        DQM = Dqm,
        Frequency = Frequency.Daily
    };

    private static MeasureMappingModel ACreateRequest() => new()
    {
        Measure = Measure,
        DQM = Dqm,
        Frequency = Frequency.Daily
    };

    private static FacilityReportingPlanModel APlanModel() => new()
    {
        Id = RowId,
        FacilityId = FacilityId,
        Measure = Measure,
        ReportingMonth = 7,
        ReportingYear = 2099
    };

    private static FacilityReportingPlan APlanEntity() => new()
    {
        Id = RowId,
        FacilityId = FacilityId,
        Measure = Measure,
        ReportingMonth = 7,
        ReportingYear = 2099
    };

    private static FacilityReportingPlanRequest APlanRequest() => new()
    {
        FacilityId = FacilityId,
        Measure = Measure,
        ReportingMonth = 7,
        ReportingYear = 2099,
        IsReporting = true
    };

    private static FacilityReportingPlanUpdateRequest APlanUpdateRequest() => new()
    {
        Id = RowId,
        FacilityId = FacilityId,
        Measure = Measure,
        ReportingMonth = 7,
        ReportingYear = 2099,
        IsReporting = false
    };
}
