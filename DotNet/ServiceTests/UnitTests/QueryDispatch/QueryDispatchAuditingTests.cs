using System.Reflection;
using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;
using QueryDispatch.Domain.Managers;

namespace UnitTests.QueryDispatch;

/// <summary>
/// What each of QueryDispatch's three configuration endpoints tells the audit mechanism, and that
/// its processing pipeline tells it nothing at all.
/// </summary>
/// <remarks>
/// The endpoint's action, resource and note live on the attribute, so they are read from it; what
/// only the handler can know - the row's id, the facility from the body, and which branch a
/// create-or-update took - is read from the audit context the handler wrote to. The event itself is
/// assembled by the shared filter, which has its own tests.
/// </remarks>
[Trait("Category", "UnitTests")]
public class QueryDispatchAuditingTests
{
    private const string ExpectedResource = "QueryDispatchConfiguration";
    private const string ConfigurationId = "d9f1c0a4-1d2e-4c3b-9a8f-6b5e4d3c2b1a";

    private readonly AutoMocker _mocker = new();
    private readonly AuditContext _audit = new();

    [Fact]
    public void TheThreeConfigurationEndpointsAreMarkedAsTheAdrSays()
    {
        AssertMarked(nameof(QueryDispatchController.CreateQueryDispatchConfigurationAsync), AuditEventType.Create);
        AssertMarked(nameof(QueryDispatchController.UpdateQueryDispatchConfiguration), AuditEventType.Update);
        AssertMarked(nameof(QueryDispatchController.DeleteQueryDispatchConfiguration), AuditEventType.Delete);
    }

    [Fact]
    public void TheConfigurationGetIsNotAudited()
    {
        //the ADR gives QueryDispatch no Query row, so the read stays unmarked
        Assert.Null(Marked(nameof(QueryDispatchController.GetFacilityConfiguration)));
    }

    [Fact]
    public async Task CreateNamesTheNewConfigurationAndTheFacilityFromTheBody()
    {
        var controller = BuildController();
        var created = new QueryDispatchConfigurationEntity
        {
            Id = ConfigurationId,
            FacilityId = QueryDispatchTestsConstants.facilityId
        };

        FacilityExists();
        ExistingConfiguration(null);
        FactoryReturns(created);

        var result = await controller.CreateQueryDispatchConfigurationAsync(
            ValidModel(QueryDispatchTestsConstants.facilityId), CancellationToken.None);

        Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(ConfigurationId, _audit.RecordId);
        Assert.Equal(QueryDispatchTestsConstants.facilityId, _audit.FacilityId);
        Assert.Null(_audit.ActionOverride);
    }

    [Fact]
    public async Task UpdateNamesTheConfigurationItChangedAndLeavesTheActionAlone()
    {
        var controller = BuildController();
        var existing = new QueryDispatchConfigurationEntity
        {
            Id = ConfigurationId,
            FacilityId = QueryDispatchTestsConstants.facilityId
        };

        FacilityExists();
        ExistingConfiguration(existing);

        var result = await controller.UpdateQueryDispatchConfiguration(
            QueryDispatchTestsConstants.facilityId, ValidModel(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result.Result);
        Assert.Equal(ConfigurationId, _audit.RecordId);

        //the facility is in the route, so the handler leaves it to the attribute
        Assert.Null(_audit.FacilityId);
        Assert.Null(_audit.ActionOverride);
    }

    [Fact]
    public async Task UpdateRecordsACreateWhenTheFacilityHadNoConfiguration()
    {
        var controller = BuildController();
        var created = new QueryDispatchConfigurationEntity
        {
            Id = ConfigurationId,
            FacilityId = QueryDispatchTestsConstants.facilityId
        };

        FacilityExists();
        ExistingConfiguration(null);
        FactoryReturns(created);

        var result = await controller.UpdateQueryDispatchConfiguration(
            QueryDispatchTestsConstants.facilityId, ValidModel(), CancellationToken.None);

        Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(AuditEventType.Create, _audit.ActionOverride);
        Assert.Equal(ConfigurationId, _audit.RecordId);
    }

    [Fact]
    public async Task DeleteNamesTheConfigurationItRemoved()
    {
        var controller = BuildController();
        ExistingConfiguration(new QueryDispatchConfigurationEntity
        {
            Id = ConfigurationId,
            FacilityId = QueryDispatchTestsConstants.facilityId
        });

        var result = await controller.DeleteQueryDispatchConfiguration(
            QueryDispatchTestsConstants.facilityId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result.Result);
        Assert.Equal(ConfigurationId, _audit.RecordId);
    }

    [Fact]
    public async Task DeleteNamesNoRowWhenTheFacilityHadNoConfiguration()
    {
        var controller = BuildController();
        ExistingConfiguration(null);

        var result = await controller.DeleteQueryDispatchConfiguration(
            QueryDispatchTestsConstants.facilityId, CancellationToken.None);

        //the endpoint answers 204 either way, so the event is published with no resource id rather
        //than falling back to the facility
        Assert.IsType<NoContentResult>(result.Result);
        Assert.Null(_audit.RecordId);
    }

    [Fact]
    public void NothingInQueryDispatchPublishesAuditEventsByHand()
    {
        var auditProducer = typeof(IProducer<string, AuditEventMessage>);
        var types = typeof(QueryDispatchController).Assembly.GetTypes();

        var takesIt = types
            .SelectMany(type => type.GetConstructors())
            .Where(constructor => constructor.GetParameters().Any(p => p.ParameterType == auditProducer))
            .Select(constructor => constructor.DeclaringType!.FullName!)
            .ToList();

        var holdsIt = types
            .SelectMany(type => type.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                               BindingFlags.Public | BindingFlags.NonPublic))
            .Where(field => field.FieldType == auditProducer)
            .Select(field => field.DeclaringType!.FullName!)
            .ToList();

        //the pipeline's own audit events are gone, so a patient event, a scheduled report and the
        //dispatch job publish none: there is no producer left for them to publish through
        Assert.Empty(takesIt);
        Assert.Empty(holdsIt);
    }

    private QueryDispatchController BuildController()
    {
        var controller = _mocker.CreateInstance<QueryDispatchController>();

        var services = new ServiceCollection();
        services.AddSingleton<IAuditContext>(_audit);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
        };

        return controller;
    }

    private static QueryDispatchConfiguration ValidModel(string? facilityId = null) => new()
    {
        FacilityId = facilityId ?? string.Empty,
        DispatchSchedules = []
    };

    private void FacilityExists() =>
        _mocker.GetMock<ITenantApiService>()
            .Setup(service => service.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private void ExistingConfiguration(QueryDispatchConfigurationEntity? configuration) =>
        _mocker.GetMock<IQueryDispatchConfigurationManager>()
            .Setup(manager => manager.GetConfigEntity(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration!);

    private void FactoryReturns(QueryDispatchConfigurationEntity configuration) =>
        _mocker.GetMock<IQueryDispatchConfigurationFactory>()
            .Setup(factory => factory.CreateQueryDispatchConfiguration(It.IsAny<string>(), It.IsAny<List<DispatchSchedule>>()))
            .Returns(configuration);

    private static AuditableAttribute? Marked(string action) =>
        typeof(QueryDispatchController).GetMethod(action)!.GetCustomAttribute<AuditableAttribute>();

    private static void AssertMarked(string action, AuditEventType expected)
    {
        var auditable = Marked(action);

        Assert.NotNull(auditable);
        Assert.Equal(expected, auditable!.Action);
        Assert.Equal(ExpectedResource, auditable.Resource);

        //the ADR's QueryDispatch rows say to remove the notes the hand-built events carried
        Assert.Null(auditable.Notes);
    }
}
