using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Controllers;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;

namespace IntegrationTests.Tenant;

/// <summary>
/// LEGLINK-1306 and LEGLINK-1337 in a real service: Tenant's own controller, manager, repository and
/// database, with the shared filter around the action and the real KafkaAuditPublisher producing into
/// the fixture's recording producer. Only the broker is stubbed.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FacilityAuditTests : IDisposable
{
    private const string CallerEmail = "nurse@example.org";

    private readonly IServiceScope _scope;
    private readonly AuditedEndpoint<FacilityController> _endpoint;
    private readonly TenantDbContext _dbContext;
    private readonly TenantIntegrationTestFixture.RecordingAuditProducer _auditProducer;

    public FacilityAuditTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var sp = _scope.ServiceProvider;

        _dbContext = sp.GetRequiredService<TenantDbContext>();
        _auditProducer = sp.GetRequiredService<TenantIntegrationTestFixture.RecordingAuditProducer>();

        var scheduleService = sp.GetRequiredService<ScheduleService>();
        scheduleService.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        _endpoint = new AuditedEndpoint<FacilityController>(
            new FacilityController(
                sp.GetRequiredService<ILogger<FacilityController>>(),
                sp.GetRequiredService<IFacilityManager>(),
                sp.GetRequiredService<IFacilityQueries>(),
                sp.GetRequiredService<IFacilityOperations>(),
                sp.GetRequiredService<IKafkaProducerFactory<string, GenerateReportValue>>(),
                sp.GetRequiredService<IOptions<ServiceRegistry>>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<LinkTokenServiceSettings>>(),
                sp.GetRequiredService<ICreateSystemToken>(),
                sp.GetRequiredService<IOptions<LinkBearerServiceOptions>>()),
            sp,
            CallerEmail);

        Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpQuartzLogProvider());
    }

    public void Dispose() => _scope.Dispose();

    private FacilityController Controller => _endpoint.Controller;

    private static RouteValueDictionary ForFacility(string facilityId) =>
        new() { ["facilityId"] = facilityId };

    private async Task<string> AFacility()
    {
        var facilityId = Guid.NewGuid().ToString();
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(new Facility
        {
            FacilityId = facilityId,
            FacilityName = $"Audit Test {facilityId}",
            TimeZone = "America/Chicago",
            ScheduledReports = new ScheduledReportModel { Daily = [], Weekly = [], Monthly = [] }
        }, CancellationToken.None);

        return facilityId;
    }

    private static FacilityModel AModelFor(string facilityId, string name) => new()
    {
        FacilityId = facilityId,
        FacilityName = name,
        TimeZone = "America/New_York",
        ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
    };

    /// <summary>
    /// Only the events for this facility and action. The recording producer is a singleton shared by
    /// every test in the collection, so it holds whatever earlier tests published too.
    /// </summary>
    private List<AuditEventMessage> EventsFor(string facilityId, AuditEventType action) =>
        _auditProducer.Produced
            .Where(produced => produced.Value.FacilityId == facilityId && produced.Value.Action == action)
            .Select(produced => produced.Value)
            .ToList();

    [Fact]
    public async Task PostFacility_PublishesACreateEventKeyedOnTheNewFacility()
    {
        var facilityId = Guid.NewGuid().ToString();

        await _endpoint.RunAsync(nameof(FacilityController.StoreFacility), new(),
            () => Controller.StoreFacility(AModelFor(facilityId, "Created Facility"), CancellationToken.None));

        var published = Assert.Single(EventsFor(facilityId, AuditEventType.Create));
        Assert.Equal("Facility", published.Resource);

        //this route carries no id, and the facility is the record, so the handler sets both.
        Assert.Equal(facilityId, published.ResourceId);
        Assert.Equal(TenantConstants.ServiceName, published.ServiceName);
        Assert.Equal(CallerEmail, published.User);
        Assert.Null(published.CorrelationId);
        Assert.Null(published.Notes);

        //a create has nothing to compare against, so the ADR asks for no property changes.
        Assert.Empty(published.PropertyChanges ?? []);
    }

    [Fact]
    public async Task PutFacility_PublishesOneUpdateEventDescribingWhatChanged()
    {
        var facilityId = await AFacility();

        await _endpoint.RunAsync<FacilityModel>(nameof(FacilityController.PutFacility), ForFacility(facilityId),
            () => Controller.PutFacility(facilityId, AModelFor(facilityId, "Updated Name"), CancellationToken.None));

        var published = Assert.Single(EventsFor(facilityId, AuditEventType.Update));
        Assert.Equal("Facility", published.Resource);
        Assert.Equal(facilityId, published.ResourceId);
        Assert.Equal(TenantConstants.ServiceName, published.ServiceName);
        Assert.Equal(CallerEmail, published.User);
        Assert.Null(published.UserId);
        Assert.Null(published.CorrelationId);
        Assert.Null(published.Notes);

        //the interceptor's half of the job: the old and new value of what the user actually changed.
        Assert.NotNull(published.PropertyChanges);
        var name = Assert.Single(published.PropertyChanges,
            change => change.PropertyName == nameof(Facility.FacilityName));
        Assert.Equal($"Audit Test {facilityId}", name.InitialPropertyValue);
        Assert.Equal("Updated Name", name.NewPropertyValue);

        //and the change is really committed.
        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Facilities.AsNoTracking()
            .SingleAsync(facility => facility.FacilityId == facilityId);
        Assert.Equal("Updated Name", stored.FacilityName);
    }

    [Fact]
    public async Task SoftDeleteFacility_PublishesASoftDeleteEvent()
    {
        var facilityId = await AFacility();

        await _endpoint.RunAsync(nameof(FacilityController.SoftDeleteFacility), ForFacility(facilityId),
            () => Controller.SoftDeleteFacility(facilityId, CancellationToken.None));

        //the action the ADR asks for, which only exists because LEGLINK-1300 added it to the enum.
        var published = Assert.Single(EventsFor(facilityId, AuditEventType.SoftDelete));
        Assert.Equal("Facility", published.Resource);
        Assert.Equal(facilityId, published.ResourceId);
        Assert.Equal(CallerEmail, published.User);

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Facilities.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(facility => facility.FacilityId == facilityId);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task RestoreFacility_PublishesARestoreEvent()
    {
        var facilityId = await AFacility();
        await _scope.ServiceProvider.GetRequiredService<IFacilityOperations>()
            .SoftDeleteAsync(facilityId, CancellationToken.None);

        await _endpoint.RunAsync(nameof(FacilityController.RestoreFacility), ForFacility(facilityId),
            () => Controller.RestoreFacility(facilityId, CancellationToken.None));

        var published = Assert.Single(EventsFor(facilityId, AuditEventType.Restore));
        Assert.Equal("Facility", published.Resource);
        Assert.Equal(facilityId, published.ResourceId);
        Assert.Equal(CallerEmail, published.User);
        Assert.Null(published.Notes);

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Facilities.AsNoTracking()
            .SingleAsync(facility => facility.FacilityId == facilityId);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteFacility_PublishesADeleteEvent()
    {
        var facilityId = await AFacility();

        await _endpoint.RunAsync(nameof(FacilityController.DeleteFacility), ForFacility(facilityId),
            () => Controller.DeleteFacility(facilityId, CancellationToken.None));

        var published = Assert.Single(EventsFor(facilityId, AuditEventType.Delete));
        Assert.Equal("Facility", published.Resource);
        Assert.Equal(facilityId, published.ResourceId);
        Assert.Equal(CallerEmail, published.User);
        Assert.Null(published.Notes);

        _dbContext.ChangeTracker.Clear();
        Assert.False(await _dbContext.Facilities.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(facility => facility.FacilityId == facilityId));
    }

    [Fact]
    public async Task AFailedUpdate_PublishesNothing()
    {
        var facilityId = await AFacility();

        //the id in the body does not match the route, which the endpoint rejects.
        await _endpoint.RunAsync<FacilityModel>(nameof(FacilityController.PutFacility), ForFacility(facilityId),
            () => Controller.PutFacility(facilityId,
                AModelFor(Guid.NewGuid().ToString(), "Should Not Be Audited"), CancellationToken.None));

        Assert.Empty(EventsFor(facilityId, AuditEventType.Update));
    }

    [Fact]
    public void TheFacilityEndpoints_AreMarkedAsTheAdrsTenantTableSays()
    {
        var post = AuditedEndpoint<FacilityController>.AttributeOn(nameof(FacilityController.StoreFacility));
        Assert.Equal(AuditEventType.Create, post.Action);
        Assert.Equal("Facility", post.Resource);

        var put = AuditedEndpoint<FacilityController>.AttributeOn(nameof(FacilityController.PutFacility));
        Assert.Equal(AuditEventType.Update, put.Action);
        Assert.Equal("Facility", put.Resource);
        Assert.Equal("facilityId", put.RecordIdRouteKey);

        var delete = AuditedEndpoint<FacilityController>.AttributeOn(nameof(FacilityController.DeleteFacility));
        Assert.Equal(AuditEventType.Delete, delete.Action);
        Assert.Equal("facilityId", delete.RecordIdRouteKey);

        //"Update Action to 'SoftDelete'. Remove Notes. Remove CorrelationId."
        var softDelete = AuditedEndpoint<FacilityController>.AttributeOn(nameof(FacilityController.SoftDeleteFacility));
        Assert.Equal(AuditEventType.SoftDelete, softDelete.Action);
        Assert.Equal("Facility", softDelete.Resource);
        Assert.Equal("facilityId", softDelete.RecordIdRouteKey);

        var restore = AuditedEndpoint<FacilityController>.AttributeOn(nameof(FacilityController.RestoreFacility));
        Assert.Equal(AuditEventType.Restore, restore.Action);
        Assert.Equal("facilityId", restore.RecordIdRouteKey);

        //the ADR keeps notes off Tenant's config events.
        Assert.All([post, put, delete, softDelete, restore], auditable => Assert.Null(auditable.Notes));
    }

    /// <summary>
    /// The two report endpoints change no configuration, so LEGLINK-1337 excludes them; this is the
    /// check that nobody marks them later.
    /// </summary>
    [Fact]
    public void TheReportEndpoints_AreNotAudited()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AuditedEndpoint<FacilityController>.AttributeOn(nameof(FacilityController.GenerateAdHocReport)));
        Assert.Throws<InvalidOperationException>(() =>
            AuditedEndpoint<FacilityController>.AttributeOn(nameof(FacilityController.RegenerateReport)));
    }
}
