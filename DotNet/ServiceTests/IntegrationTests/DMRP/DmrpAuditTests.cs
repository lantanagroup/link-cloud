using IntegrationTests.Tenant;
using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DMRP;

/// <summary>
/// LEGLINK-1343: the nine DMRP endpoints the story marks auditable, and the two write paths it says
/// must stay silent. DMRP adds no audit plumbing of its own - it runs inside Tenant and publishes
/// through Tenant's registration - so what these tests cover is the markings, what each handler fills
/// in that the route cannot, and the absence of events everywhere else.
/// </summary>
/// <remarks>
/// The markings are read off the real controller methods rather than restated, so a story row and an
/// endpoint that stop agreeing fail here rather than in QA.
/// </remarks>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class DmrpAuditTests : IDisposable
{
    private const string CallerEmail = "dmrp.admin@example.org";
    private const string FacilityId = "900";
    private const int Month = 5;
    private const int Year = 2026;
    private const string Dqm = "NHSNAcuteCareHospitalMonthlyInitialPopulation";

    private readonly DmrpIntegrationTestFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _services;
    private readonly TenantIntegrationTestFixture.RecordingAuditProducer _auditProducer;
    private readonly IEntityRepository<MeasureMapping> _mappings;
    private readonly IEntityRepository<FacilityReportingPlan> _plans;

    /// <summary>Stands in for the DMRP API, so the real sync has a plan to write.</summary>
    private readonly Mock<IDmrpApiClient> _dmrpApi = new();

    /// <summary>
    /// Answers for any dQM, since what these tests are about is the event rather than the validation
    /// in front of it.
    /// </summary>
    private readonly Mock<IMeasureEvalServiceClient> _measureEval = new();

    public DmrpAuditTests(DmrpIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _scope = fixture.ServiceProvider.CreateScope();
        _services = _scope.ServiceProvider;

        _auditProducer = _services.GetRequiredService<TenantIntegrationTestFixture.RecordingAuditProducer>();
        _mappings = _services.GetRequiredService<IEntityRepository<MeasureMapping>>();
        _plans = _services.GetRequiredService<IEntityRepository<FacilityReportingPlan>>();

        _measureEval
            .Setup(client => client.GetMeasureDefinitionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string> { StatusCode = StatusCodes.Status200OK });

        _fixture.ResetFacilityExistence();
        _fixture.ResetFacilityDirectory();

        // One SQLite database is shared by every DMRP test class, and deleting all measure mappings is
        // refused while a reporting plan references one, so the rows go in this order both ways.
        ClearRows();
    }

    public void Dispose()
    {
        ClearRows();
        _scope.Dispose();
    }

    // ---- the markings ----

    /// <summary>
    /// The story's four measure-mapping rows. The note on the by-id delete names values the row
    /// carries, so the handler has to read it before the delete takes it away; the bulk delete says
    /// what it cleared instead, because it names no single row.
    /// </summary>
    [Fact]
    public void TheMeasureMappingEndpoints_AreMarkedAsTheStorysRowsSay()
    {
        var create = MappingAttribute(nameof(MeasureMappingsController.CreateMeasureMapping));
        Assert.Equal(AuditEventType.Create, create.Action);
        Assert.Equal("MeasureMappings", create.Resource);
        Assert.Null(create.Notes);

        var update = MappingAttribute(nameof(MeasureMappingsController.UpdateMeasureMapping));
        Assert.Equal(AuditEventType.Update, update.Action);
        Assert.Equal("MeasureMappings", update.Resource);
        Assert.Null(update.Notes);

        var delete = MappingAttribute(nameof(MeasureMappingsController.DeleteMeasureMapping));
        Assert.Equal(AuditEventType.Delete, delete.Action);
        Assert.Equal("MeasureMappings", delete.Resource);
        Assert.Equal("Measure: {Measure}, DQM: {dqm}, Frequency: {Frequency}", delete.Notes);

        var deleteAll = MappingAttribute(nameof(MeasureMappingsController.DeleteAllMeasureMappings));
        Assert.Equal(AuditEventType.Delete, deleteAll.Action);
        Assert.Equal("MeasureMappings", deleteAll.Resource);
        Assert.Equal("All measure mappings deleted", deleteAll.Notes);
    }

    /// <summary>
    /// The story's five reporting-plan rows, including the two bulk deletions that say what they
    /// cleared rather than naming a row.
    /// </summary>
    [Fact]
    public void TheReportingPlanEndpoints_AreMarkedAsTheStorysRowsSay()
    {
        var create = PlanAttribute(nameof(FacilityReportingPlansController.CreateFacilityReportingPlan));
        Assert.Equal(AuditEventType.Create, create.Action);
        Assert.Equal("FacilityReportingPlans", create.Resource);
        Assert.Null(create.Notes);

        var update = PlanAttribute(nameof(FacilityReportingPlansController.UpdateFacilityReportingPlan));
        Assert.Equal(AuditEventType.Update, update.Action);
        Assert.Equal("FacilityReportingPlans", update.Resource);
        Assert.Null(update.Notes);

        var delete = PlanAttribute(nameof(FacilityReportingPlansController.DeleteFacilityReportingPlan));
        Assert.Equal(AuditEventType.Delete, delete.Action);
        Assert.Equal("FacilityReportingPlans", delete.Resource);
        Assert.Equal("Reporting Plan for {Measure} (Month: {month}, Year: {year}) deleted", delete.Notes);

        var deleteAll = PlanAttribute(nameof(FacilityReportingPlansController.DeleteFacilityReportingPlans));
        Assert.Equal(AuditEventType.Delete, deleteAll.Action);
        Assert.Equal("FacilityReportingPlans", deleteAll.Resource);
        Assert.Equal("All reporting plans deleted", deleteAll.Notes);

        var forFacility = PlanAttribute(nameof(FacilityReportingPlansController.DeleteFacilityReportingPlansForFacility));
        Assert.Equal(AuditEventType.Delete, forFacility.Action);
        Assert.Equal("FacilityReportingPlans", forFacility.Resource);
        Assert.Equal("All reporting plans for facility deleted", forFacility.Notes);
    }

    // ---- measure mappings ----

    [Fact]
    public async Task PostMeasureMapping_PublishesACreateEventNamingTheNewRow()
    {
        var measure = AMeasure();
        MeasureMappingModel? created = null;

        await MappingRequest(nameof(MeasureMappingsController.CreateMeasureMapping), new(),
            async controller =>
            {
                var result = await controller.CreateMeasureMapping(new MeasureMappingModel
                {
                    Measure = measure,
                    DQM = Dqm,
                    Frequency = Frequency.Monthly
                }, CancellationToken.None);

                created = (result as CreatedResult)?.Value as MeasureMappingModel;
                return result;
            });

        var published = Assert.Single(Events("MeasureMappings"));
        Assert.Equal(AuditEventType.Create, published.Value.Action);

        //the route carries no id on a create, so the handler sets it from the row it just made.
        Assert.Equal(created!.Id, published.Value.ResourceId);
        Assert.Equal(CallerEmail, published.Value.User);
        Assert.Null(published.Value.Notes);

        //a measure mapping belongs to no facility, so the event names none - and the Kafka key, which
        //is the facility, is empty.
        Assert.Null(published.Value.FacilityId);
        Assert.Equal(string.Empty, published.Key);
    }

    /// <summary>
    /// The dQM change has to be on the event, which it is only because the property-change
    /// interceptor is attached to the context the handler writes through.
    /// </summary>
    [Fact]
    public async Task PutMeasureMapping_PublishesAnUpdateEventListingTheDqmChange()
    {
        var mapping = await AMappingAsync();

        //read before the update: the manager mutates the row it loaded, and in a shared context that
        //would be this instance.
        var (id, measure, originalDqm) = (mapping.Id, mapping.Measure, mapping.DQM);
        const string newDqm = "NHSNGlycemicControlHypoglycemicInitialPopulation";

        await MappingRequest(nameof(MeasureMappingsController.UpdateMeasureMapping),
            Route("id", id),
            controller => controller.UpdateMeasureMapping(id, new MeasureMappingModel
            {
                Id = id,
                Measure = measure,
                DQM = newDqm,
                Frequency = Frequency.Monthly
            }, CancellationToken.None));

        var published = TheMappingEvent();
        Assert.Equal(AuditEventType.Update, published.Action);
        Assert.Equal(id, published.ResourceId);
        Assert.Equal(CallerEmail, published.User);

        var changed = Assert.Single(published.PropertyChanges!,
            change => change.PropertyName == nameof(MeasureMapping.DQM));
        Assert.Equal(originalDqm, changed.InitialPropertyValue);
        Assert.Equal(newDqm, changed.NewPropertyValue);
    }

    [Fact]
    public async Task DeleteMeasureMapping_PublishesANoteNamingTheMeasureDqmAndFrequency()
    {
        var mapping = await AMappingAsync();
        var (id, measure) = (mapping.Id, mapping.Measure);

        await MappingRequest(nameof(MeasureMappingsController.DeleteMeasureMapping),
            Route("id", id),
            controller => controller.DeleteMeasureMapping(id, CancellationToken.None));

        var published = TheMappingEvent();
        Assert.Equal(AuditEventType.Delete, published.Action);
        Assert.Equal(id, published.ResourceId);

        //the story's note, filled from the row the handler read before deleting it: nothing is left to
        //look the measure, dQM or frequency up from afterwards.
        Assert.Equal($"Measure: {measure}, DQM: {Dqm}, Frequency: {Frequency.Monthly}", published.Notes);
        Assert.Empty(await _mappings.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAllMeasureMappings_PublishesOneBulkEventNamingNoRow()
    {
        await AMappingAsync();
        await AMappingAsync();

        await MappingRequest(nameof(MeasureMappingsController.DeleteAllMeasureMappings), new(),
            controller => controller.DeleteAllMeasureMappings(CancellationToken.None));

        var published = TheMappingEvent();
        Assert.Equal(AuditEventType.Delete, published.Action);
        Assert.Equal("All measure mappings deleted", published.Notes);

        //a deletion that clears many rows identifies none of them, and never falls back to something
        //else just to have an id to put there.
        Assert.Null(published.ResourceId);
        Assert.Empty(await _mappings.GetAllAsync());
    }

    // ---- reporting plans ----

    [Fact]
    public async Task PostReportingPlan_PublishesACreateEventCarryingTheRowAndTheFacility()
    {
        var mapping = await AMappingAsync();
        FacilityReportingPlanModel? created = null;

        await PlanRequest(nameof(FacilityReportingPlansController.CreateFacilityReportingPlan), new(),
            async controller =>
            {
                var result = await controller.CreateFacilityReportingPlan(APlanRequest(mapping.Id),
                    CancellationToken.None);

                created = (result as CreatedResult)?.Value as FacilityReportingPlanModel;
                return result;
            });

        var published = Assert.Single(Events("FacilityReportingPlans"));
        Assert.Equal(AuditEventType.Create, published.Value.Action);
        Assert.Equal(created!.Id, published.Value.ResourceId);
        Assert.Equal(CallerEmail, published.Value.User);

        //the facility is in the body on this route rather than the route itself, so the handler sets
        //it - and it is also the Kafka key.
        Assert.Equal(FacilityId, published.Value.FacilityId);
        Assert.Equal(FacilityId, published.Key);
    }

    [Fact]
    public async Task PutReportingPlan_PublishesAnUpdateEventCarryingTheFacility()
    {
        var plan = await APlanAsync();

        await PlanRequest(nameof(FacilityReportingPlansController.UpdateFacilityReportingPlan),
            Route("id", plan.Id!),
            controller => controller.UpdateFacilityReportingPlan(plan.Id!,
                new FacilityReportingPlanUpdateRequest
                {
                    Id = plan.Id,
                    FacilityId = FacilityId,
                    MeasureMappingId = plan.MeasureMappingId,
                    ReportingMonth = Month,
                    ReportingYear = Year,
                    IsReporting = false
                }, CancellationToken.None));

        var published = ThePlanEvent();
        Assert.Equal(AuditEventType.Update, published.Action);
        Assert.Equal(plan.Id, published.ResourceId);

        //the route carries the plan's id, not the facility's, so the handler reads the facility off
        //the row - every reporting-plan event carries one.
        Assert.Equal(FacilityId, published.FacilityId);

        var changed = Assert.Single(published.PropertyChanges!,
            change => change.PropertyName == nameof(FacilityReportingPlan.IsReporting));
        Assert.Equal(bool.TrueString, changed.InitialPropertyValue);
        Assert.Equal(bool.FalseString, changed.NewPropertyValue);
    }

    [Fact]
    public async Task DeleteReportingPlan_PublishesANoteNamingTheMeasureMonthAndYear()
    {
        var plan = await APlanAsync();

        await PlanRequest(nameof(FacilityReportingPlansController.DeleteFacilityReportingPlan),
            Route("id", plan.Id!),
            controller => controller.DeleteFacilityReportingPlan(plan.Id!, CancellationToken.None));

        var published = ThePlanEvent();
        Assert.Equal(AuditEventType.Delete, published.Action);
        Assert.Equal(plan.Id, published.ResourceId);
        Assert.Equal(FacilityId, published.FacilityId);
        Assert.Equal($"Reporting Plan for {plan.Measure} (Month: {Month}, Year: {Year}) deleted",
            published.Notes);
    }

    [Fact]
    public async Task DeleteReportingPlansForFacility_PublishesABulkEventCarryingTheFacilityButNoRow()
    {
        await APlanAsync();

        await PlanRequest(nameof(FacilityReportingPlansController.DeleteFacilityReportingPlansForFacility),
            Route("facilityId", FacilityId),
            controller => controller.DeleteFacilityReportingPlansForFacility(FacilityId, CancellationToken.None));

        var published = ThePlanEvent();
        Assert.Equal(AuditEventType.Delete, published.Action);
        Assert.Equal("All reporting plans for facility deleted", published.Notes);

        //the facility comes off the route here; the rows it cleared are many, so none is named.
        Assert.Equal(FacilityId, published.FacilityId);
        Assert.Null(published.ResourceId);
        Assert.Empty(await _plans.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAllReportingPlans_PublishesABulkEventNamingNeitherRowNorFacility()
    {
        await APlanAsync();

        await PlanRequest(nameof(FacilityReportingPlansController.DeleteFacilityReportingPlans), new(),
            controller => controller.DeleteFacilityReportingPlans(CancellationToken.None));

        var published = ThePlanEvent();
        Assert.Equal(AuditEventType.Delete, published.Action);
        Assert.Equal("All reporting plans deleted", published.Notes);
        Assert.Null(published.ResourceId);

        //this one clears every facility's plans, so there is no one facility to name either.
        Assert.Null(published.FacilityId);
        Assert.Empty(await _plans.GetAllAsync());
    }

    /// <summary>
    /// The event names the service the host is configured as, which in production is Tenant - DMRP has
    /// no host of its own there, and this fixture stands in for Tenant's.
    /// </summary>
    [Fact]
    public async Task AnEvent_NamesTheConfiguredHostServiceAndTheCaller()
    {
        var information = _services.GetRequiredService<ServiceInformation>();
        var expected = string.IsNullOrWhiteSpace(information.ServiceConfigName)
            ? information.ServiceName
            : information.ServiceConfigName;

        await PlanRequest(nameof(FacilityReportingPlansController.DeleteFacilityReportingPlans), new(),
            controller => controller.DeleteFacilityReportingPlans(CancellationToken.None));

        var published = ThePlanEvent();
        Assert.Equal(expected, published.ServiceName);
        Assert.Equal(CallerEmail, published.User);
        Assert.NotNull(published.EventDate);
    }

    // ---- the write paths that must stay silent ----

    /// <summary>
    /// The nightly sync writes reporting plans and records nothing: the mechanism publishes from the
    /// endpoint filter, and the sync is not an endpoint. Driven through the real sync over a stubbed
    /// DMRP API, so the write is the one the nightly job performs.
    /// </summary>
    [Fact]
    public async Task TheNightlySync_WritesPlansAndPublishesNothing()
    {
        var mapping = await AMappingAsync();
        TheDmrpApiReturns(mapping.Measure);

        var result = await TheRealSync().SyncAsync(FacilityId, Month, Year, CancellationToken.None);

        //it wrote - otherwise the silence below would prove nothing.
        Assert.Equal(1, result.Recorded);
        Assert.NotEmpty(await _plans.GetAllAsync());

        Assert.Empty(_auditProducer.Produced);
    }

    /// <summary>
    /// The refreshing lookup writes the same rows through the same sync, on a GET the story leaves
    /// unmarked, and so records nothing either.
    /// </summary>
    [Fact]
    public async Task TheRefreshingLookup_WritesPlansAndPublishesNothing()
    {
        var mapping = await AMappingAsync();
        TheDmrpApiReturns(mapping.Measure);

        var result = await PlanController(_services, TheRealSync())
            .GetFacilityReportingPlansForFacility(FacilityId, Month, Year,
                isReporting: null, monthsAhead: null, refresh: true, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotEmpty(await _plans.GetAllAsync());

        Assert.Empty(_auditProducer.Produced);
    }

    // ---- running one audited request ----

    /// <summary>
    /// One audited call, in a scope of its own. <see cref="IAuditContext"/> is scoped - one per
    /// request in the running service - so sharing a scope between calls would let what one handler
    /// set reach the next one's event, and a bulk delete would inherit the create's record id.
    /// </summary>
    private async Task MappingRequest(string action, RouteValueDictionary routeValues,
        Func<MeasureMappingsController, Task<IActionResult>> call)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var endpoint = new AuditedEndpoint<MeasureMappingsController>(
            new MeasureMappingsController(
                scope.ServiceProvider.GetRequiredService<ILogger<MeasureMappingsController>>(),
                scope.ServiceProvider.GetRequiredService<IMeasureMappingManager>(),
                scope.ServiceProvider.GetRequiredService<IMeasureMappingQueries>(),
                _measureEval.Object),
            scope.ServiceProvider,
            CallerEmail);

        await endpoint.RunAsync(action, routeValues, () => call(endpoint.Controller));
    }

    private async Task PlanRequest(string action, RouteValueDictionary routeValues,
        Func<FacilityReportingPlansController, Task<IActionResult>> call)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var endpoint = new AuditedEndpoint<FacilityReportingPlansController>(
            PlanController(scope.ServiceProvider,
                scope.ServiceProvider.GetRequiredService<IDmrpReportingPlanSync>()),
            scope.ServiceProvider,
            CallerEmail);

        await endpoint.RunAsync(action, routeValues, () => call(endpoint.Controller));
    }

    private static RouteValueDictionary Route(string key, string value) => new() { [key] = value };

    private static AuditableAttribute MappingAttribute(string action) =>
        AuditedEndpoint<MeasureMappingsController>.AttributeOn(action);

    private static AuditableAttribute PlanAttribute(string action) =>
        AuditedEndpoint<FacilityReportingPlansController>.AttributeOn(action);

    private FacilityReportingPlansController PlanController(IServiceProvider services,
        IDmrpReportingPlanSync sync) =>
        new(
            services.GetRequiredService<ILogger<FacilityReportingPlansController>>(),
            services.GetRequiredService<IFacilityReportingPlanManager>(),
            services.GetRequiredService<IFacilityReportingPlanQueries>(),
            services.GetRequiredService<IFacilityReportingPlanLookAhead>(),
            sync,
            _fixture.FacilityExistenceMock.Object,
            services.GetRequiredService<IFacilityReportingPeriodResolver>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    // ---- fixture helpers ----

    private List<(string Key, AuditEventMessage Value)> Events(string resource) =>
        _auditProducer.Produced.Where(produced => produced.Value.Resource == resource).ToList();

    private AuditEventMessage TheMappingEvent() => Assert.Single(Events("MeasureMappings")).Value;

    private AuditEventMessage ThePlanEvent() => Assert.Single(Events("FacilityReportingPlans")).Value;

    /// <summary>Measure is required and (Measure, dQM) is unique, so each row needs its own.</summary>
    private static string AMeasure() => $"MEASURE-{Guid.NewGuid():N}";

    private FacilityReportingPlanRequest APlanRequest(string measureMappingId) => new()
    {
        FacilityId = FacilityId,
        MeasureMappingId = measureMappingId,
        ReportingMonth = Month,
        ReportingYear = Year,
        IsReporting = true
    };

    private void TheDmrpApiReturns(string measure)
    {
        _dmrpApi
            .Setup(client => client.GetReportingPlanAsync(FacilityId, Month, Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DmrpReportingPlanEntry(ReportingComponents.Msc, measure, Month, Year)]);
    }

    /// <summary>Seeded directly: the mapping is a precondition here, not the thing under test.</summary>
    private async Task<MeasureMapping> AMappingAsync()
    {
        var mapping = new MeasureMapping
        {
            Measure = AMeasure(),
            DQM = Dqm,
            Frequency = Frequency.Monthly
        };

        await _mappings.AddAsync(mapping);
        await _mappings.SaveChangesAsync();
        _auditProducer.Clear();

        return mapping;
    }

    /// <summary>
    /// A plan created through the endpoint, because the create is what derives the measure the delete
    /// note names. The event it published is cleared, so a test sees only its own.
    /// </summary>
    private async Task<FacilityReportingPlanModel> APlanAsync()
    {
        var mapping = await AMappingAsync();
        FacilityReportingPlanModel? created = null;

        await PlanRequest(nameof(FacilityReportingPlansController.CreateFacilityReportingPlan), new(),
            async controller =>
            {
                var result = await controller.CreateFacilityReportingPlan(APlanRequest(mapping.Id),
                    CancellationToken.None);

                created = Assert.IsType<FacilityReportingPlanModel>(Assert.IsType<CreatedResult>(result).Value);
                return result;
            });

        _auditProducer.Clear();

        return created!;
    }

    private DmrpReportingPlanSync TheRealSync() =>
        new(_dmrpApi.Object, _plans, _mappings,
            _services.GetRequiredService<ILogger<DmrpReportingPlanSync>>());

    private void ClearRows()
    {
        foreach (var plan in _plans.GetAllAsync().GetAwaiter().GetResult())
        {
            _plans.Remove(plan);
        }

        _plans.SaveChangesAsync().GetAwaiter().GetResult();

        foreach (var mapping in _mappings.GetAllAsync().GetAwaiter().GetResult())
        {
            _mappings.Remove(mapping);
        }

        _mappings.SaveChangesAsync().GetAwaiter().GetResult();
        _auditProducer.Clear();
    }
}
