using DataAcquisition.Domain.Application.Models;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

/// <summary>
/// Guards the move of every Data Acquisition route to /api/data-acquisition: no controller may stay on
/// the old prefix, and every create endpoint's Location header must resolve under the new one.
/// </summary>
/// <remarks>
/// The Location tests call each create action directly, then resolve the route values it returned
/// through the real link generator, with every Data Acquisition controller mapped. That catches the
/// mistakes a type check on the result can't: a wrong CreatedAtAction overload, a route value whose
/// name doesn't match the template, or an action name that no longer exists.
/// </remarks>
[Trait("Category", "IntegrationTests")]
public class DataAcquisitionRoutePrefixTests : IClassFixture<QueryPlanConfigApiFactory>
{
    private const string CanonicalPrefix = "/api/data-acquisition/";
    private const string FacilityId = "facility-1";

    // Real MVC services so EncounterMapping's TryValidateModel runs against the DataAnnotations validator.
    private static readonly IServiceProvider MvcServices = BuildMvcServices();

    private readonly QueryPlanConfigApiFactory _factory;

    public DataAcquisitionRoutePrefixTests(QueryPlanConfigApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Endpoints_EveryControllerAction_UsesCanonicalPrefix()
    {
        var offenders = GetControllerEndpoints()
            .Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'))
            .Where(path => !path.StartsWith(CanonicalPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        offenders.Should().BeEmpty("every Data Acquisition route must live under {0}", CanonicalPrefix);
    }

    [Fact]
    public void Endpoints_EveryController_IsMapped()
    {
        // Keeps the prefix test honest: it would pass vacuously if the factory stopped loading the controllers.
        var controllerNames = typeof(QueryPlanConfigController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToList();

        var mappedNames = GetControllerEndpoints()
            .Select(e => e.Metadata.GetMetadata<ControllerActionDescriptor>()!.ControllerTypeInfo.Name)
            .Distinct()
            .ToList();

        controllerNames.Should().NotBeEmpty();
        mappedNames.Should().BeEquivalentTo(controllerNames);
    }

    [Fact]
    public async Task CreateSftpLog_Success_LocationResolvesToCreatedLog()
    {
        var logId = Guid.NewGuid();
        var mocker = new AutoMocker();
        mocker.GetMock<IValidator<CreateSftpLogRequest>>()
            .Setup(v => v.ValidateAsync(It.IsAny<CreateSftpLogRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        mocker.GetMock<ITenantApiService>()
            .Setup(t => t.CheckFacilityExists(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mocker.GetMock<ISftpAcquisitionLogManager>()
            .Setup(m => m.CreateAsync(It.IsAny<SftpAcquisitionLogModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SftpAcquisitionLogModel { ExternalId = logId, FacilityId = FacilityId });
        var controller = WithHttpContext(mocker.CreateInstance<SftpLogController>());

        var result = await controller.CreateSftpLog(new CreateSftpLogRequest(FacilityId, SftpAcquisitionType.Census),
                                                    CancellationToken.None);

        ResolveLocation(result, "SftpLog").Should().Be($"/api/data-acquisition/sftp-logs/{logId}");
    }

    [Fact]
    public async Task CreateEncounterMapping_Success_LocationResolvesToCreatedMapping()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()))
            .ReturnsAsync(new EncounterMappingModel
            {
                EncounterMappingId = 7,
                FacilityId = FacilityId,
                EncounterId = "encounter-1",
                PatientId = "patient-1"
            });
        var controller = mocker.CreateInstance<EncounterMappingController>();
        controller.ControllerContext = new ControllerContext
        {
            ActionDescriptor = new ControllerActionDescriptor(),
            HttpContext = new DefaultHttpContext { RequestServices = MvcServices }
        };
        controller.ObjectValidator = MvcServices.GetRequiredService<IObjectModelValidator>();
        controller.ProblemDetailsFactory = MvcServices.GetRequiredService<ProblemDetailsFactory>();

        var result = await controller.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = FacilityId,
            EncounterId = "encounter-1",
            PatientId = "patient-1"
        });

        ResolveLocation(result, "EncounterMapping").Should().Be("/api/data-acquisition/encounter-mappings/7");
    }

    [Fact]
    public async Task CreateOrganizationLocationConfiguration_Success_LocationResolvesToCreatedConfig()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationConfigurationModel>(),
                                      It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationLocationConfigurationModel { ConfigId = 42, FacilityId = FacilityId });
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.CreateAsync(FacilityId, new CreateOrganizationLocationConfigurationApiModel
        {
            Description = "Config",
            IsActive = true,
            Conditions = []
        });

        ResolveLocation(result, "OrganizationLocationConfiguration")
            .Should()
            .Be("/api/data-acquisition/location-config/42");
    }

    [Fact]
    public async Task CreateQueryPlan_Success_LocationResolvesToFacilityQueryPlan()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IQueryPlanManager>()
            .Setup(m => m.AddAsync(It.IsAny<CreateQueryPlanModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryPlanModel { FacilityId = FacilityId, Type = Frequency.Monthly });
        var controller = mocker.CreateInstance<QueryPlanConfigController>();

        var result = await controller.CreateQueryPlan(FacilityId,
                                                      new QueryPlanApiModel
                                                      {
                                                          FacilityId = FacilityId,
                                                          Type = Frequency.Monthly,
                                                          PlanName = "Test",
                                                          InitialQueries = new Dictionary<string, IQueryConfig>
                                                          {
                                                              { "1", new ParameterQueryConfig { Parameters = [] } }
                                                          },
                                                          SupplementalQueries = new Dictionary<string, IQueryConfig>
                                                          {
                                                              { "1", new ParameterQueryConfig { Parameters = [] } }
                                                          }
                                                      },
                                                      CancellationToken.None);

        ResolveLocation(result, "QueryPlanConfig").Should().Be($"/api/data-acquisition/{FacilityId}/QueryPlan");
    }

    [Fact]
    public async Task CreateSftpConfiguration_Success_LocationResolvesToOrganizationConfiguration()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IValidator<CreateSftpConfigurationModel>>()
            .Setup(v => v.ValidateAsync(It.IsAny<CreateSftpConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        mocker.GetMock<ITenantApiService>()
            .Setup(t => t.CheckFacilityExists(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mocker.GetMock<ISftpConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<SftpConfigurationModel>(), FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SftpConfigurationModel { OrganizationId = FacilityId });
        var controller = WithHttpContext(mocker.CreateInstance<SftpConfigurationController>());

        var result = await controller.CreateSftpConfiguration(FacilityId,
                                                              new CreateSftpConfigurationModel(),
                                                              CancellationToken.None);

        ResolveLocation(result.Result!, "SftpConfiguration")
            .Should()
            .Be($"/api/data-acquisition/{FacilityId}/sftp-configurations");
    }

    [Fact]
    public async Task CreateAuthenticationSettings_Success_LocationUsesCanonicalPrefix()
    {
        // The Location points back at the POST action itself, with the request body as route values.
        // That predates the prefix move and isn't fixed here, so only the prefix and path are asserted.
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirQueryConfigurationManager>()
            .Setup(m => m.CreateAuthenticationConfiguration(It.IsAny<string>(),
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationConfigurationModel());
        var controller = mocker.CreateInstance<AuthenticationConfigController>();

        var result = await controller.CreateAuthenticationSettings(FacilityId,
                                                                   QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                                                                   new AuthenticationConfigurationModel(),
                                                                   CancellationToken.None);

        ResolveLocation(result.Result!, "AuthenticationConfig")
            .Should()
            .StartWith($"/api/data-acquisition/{FacilityId}/fhirQueryConfiguration/authentication");
    }

    [Fact]
    public async Task CreateFhirConfiguration_Success_LocationUsesCanonicalPrefix()
    {
        // Same pre-existing shape as CreateAuthenticationSettings: the Location is the POST route itself.
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirQueryConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateFhirQueryConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirQueryConfigurationModel());
        var controller = mocker.CreateInstance<QueryConfigController>();

        var result = await controller.CreateFhirConfiguration(new ApiCreateFhirQueryConfigurationModel(),
                                                              CancellationToken.None);

        ResolveLocation(result.Result!, "QueryConfig")
            .Should()
            .StartWith("/api/data-acquisition/fhirQueryConfiguration");
    }

    private List<RouteEndpoint> GetControllerEndpoints()
    {
        // The factory also discovers probe controllers declared in this test assembly; only the service's own count.
        var serviceAssembly = typeof(QueryPlanConfigController).Assembly;

        return _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.Assembly == serviceAssembly)
            .ToList();
    }

    /// <summary>
    /// Generates the Location path the created result would produce, using the app's real routes.
    /// Returns null when the route values don't satisfy the target template.
    /// </summary>
    private string? ResolveLocation(IActionResult result, string controllerName)
    {
        var linkGenerator = _factory.Services.GetRequiredService<LinkGenerator>();

        return result switch
        {
            CreatedAtActionResult created => linkGenerator.GetPathByAction(created.ActionName!,
                                                                           created.ControllerName ?? controllerName,
                                                                           created.RouteValues),
            CreatedAtRouteResult created => linkGenerator.GetPathByRouteValues(created.RouteName,
                                                                               created.RouteValues),
            _ => throw new Xunit.Sdk.XunitException($"Expected a created result, got {result.GetType().Name}.")
        };
    }

    private static TController WithHttpContext<TController>(TController controller)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static IServiceProvider BuildMvcServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }
}
