using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Microsoft.Extensions.Logging;
using Moq;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class AcquisitionDependencyCheckerTests
{
    private readonly Mock<IQueryPlanQueries> _queryPlanQueries = new();
    private readonly Mock<IDataAcquisitionLogQueries> _logQueries = new();
    private readonly Mock<ILogger<AcquisitionDependencyChecker>> _logger = new();

    private AcquisitionDependencyChecker CreateChecker() =>
        new(_queryPlanQueries.Object, _logQueries.Object, _logger.Object);

    private static DataAcquisitionLogModel CreateLog(
        QueryPhase phase = QueryPhase.Initial,
        string correlationId = "corr-1",
        string facilityId = "facility-1",
        params ResourceType[] logResourceTypes)
    {
        return new DataAcquisitionLogModel
        {
            Id = 42,
            FacilityId = facilityId,
            CorrelationId = correlationId,
            QueryPhase = phase,
            ScheduledReport = new ScheduledReport { Frequency = Frequency.Discharge },
            FhirQuery = new List<FhirQueryModel>
            {
                new() { ResourceTypes = logResourceTypes.ToList() }
            }
        };
    }

    private static QueryPlanModel CreatePlan(
        string resourceType,
        params string[] dependencyResourceTypes)
    {
        var parameters = dependencyResourceTypes
            .Select(r => (IParameter)new ResourceIdsParameter { Name = "patient", Resource = r })
            .ToList();

        var config = new ParameterQueryConfig
        {
            ResourceType = resourceType,
            Parameters = parameters
        };

        return new QueryPlanModel
        {
            FacilityId = "facility-1",
            Type = Frequency.Discharge,
            InitialQueries = new Dictionary<string, IQueryConfig> { ["q1"] = config },
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
    }

    [Fact]
    public async Task CheckDependenciesAsync_NoCorrelationId_ReturnsMet()
    {
        var log = CreateLog(correlationId: "");

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        Assert.Empty(result.BlockingResourceTypes);
        _queryPlanQueries.Verify(q => q.GetAsync(It.IsAny<string>(), It.IsAny<Frequency>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckDependenciesAsync_NoQueryPlan_ReturnsMet()
    {
        var log = CreateLog(logResourceTypes: ResourceType.Condition);
        _queryPlanQueries
            .Setup(q => q.GetAsync("facility-1", Frequency.Discharge, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryPlanModel?)null);

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        _logQueries.Verify(q => q.GetNonTerminalDependencyResourceTypes(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckDependenciesAsync_NoResourceIdsParameters_ReturnsMet()
    {
        var log = CreateLog(logResourceTypes: ResourceType.Condition);
        var plan = CreatePlan("Condition"); // no dependency resource types

        _queryPlanQueries
            .Setup(q => q.GetAsync("facility-1", Frequency.Discharge, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        _logQueries.Verify(q => q.GetNonTerminalDependencyResourceTypes(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckDependenciesAsync_ResourceTypeMismatch_ReturnsMet()
    {
        // Log covers Observation but plan only defines ResourceIds dependency for Condition.
        var log = CreateLog(logResourceTypes: ResourceType.Observation);
        var plan = CreatePlan("Condition", "Encounter");

        _queryPlanQueries
            .Setup(q => q.GetAsync("facility-1", Frequency.Discharge, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        _logQueries.Verify(q => q.GetNonTerminalDependencyResourceTypes(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckDependenciesAsync_AllDependenciesTerminal_ReturnsMet()
    {
        var log = CreateLog(logResourceTypes: ResourceType.Condition);
        var plan = CreatePlan("Condition", "Encounter");

        _queryPlanQueries
            .Setup(q => q.GetAsync("facility-1", Frequency.Discharge, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _logQueries
            .Setup(q => q.GetNonTerminalDependencyResourceTypes(
                "corr-1", "facility-1",
                It.Is<List<string>>(l => l.Count == 1 && l[0] == "Encounter"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        Assert.Empty(result.BlockingResourceTypes);
    }

    [Fact]
    public async Task CheckDependenciesAsync_NonTerminalDependencyExists_ReturnsBlocking()
    {
        var log = CreateLog(logResourceTypes: ResourceType.Condition);
        var plan = CreatePlan("Condition", "Encounter");

        _queryPlanQueries
            .Setup(q => q.GetAsync("facility-1", Frequency.Discharge, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _logQueries
            .Setup(q => q.GetNonTerminalDependencyResourceTypes(
                "corr-1", "facility-1",
                It.Is<List<string>>(l => l.Contains("Encounter")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Encounter" });

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.False(result.AreDependenciesMet);
        Assert.Single(result.BlockingResourceTypes);
        Assert.Contains("Encounter", result.BlockingResourceTypes);
    }

    [Fact]
    public async Task CheckDependenciesAsync_MultipleDependencies_OnlyBlockingReturned()
    {
        var log = CreateLog(logResourceTypes: ResourceType.Condition);
        var plan = CreatePlan("Condition", "Encounter", "Patient");

        _queryPlanQueries
            .Setup(q => q.GetAsync("facility-1", Frequency.Discharge, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _logQueries
            .Setup(q => q.GetNonTerminalDependencyResourceTypes(
                "corr-1", "facility-1",
                It.Is<List<string>>(l => l.Count == 2 && l.Contains("Encounter") && l.Contains("Patient")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Encounter" });

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.False(result.AreDependenciesMet);
        Assert.Single(result.BlockingResourceTypes);
        Assert.Contains("Encounter", result.BlockingResourceTypes);
    }

    [Fact]
    public async Task CheckDependenciesAsync_ReferentialPhase_ReturnsMet()
    {
        var log = CreateLog(phase: QueryPhase.Referential, logResourceTypes: ResourceType.Condition);

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        _queryPlanQueries.Verify(q => q.GetAsync(It.IsAny<string>(), It.IsAny<Frequency>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckDependenciesAsync_UsesReportableEventWhenAvailable()
    {
        var log = CreateLog(logResourceTypes: ResourceType.Condition);
        log.ReportableEvent = ReportableEvent.EOM;  // should map to Frequency.Monthly
        log.ScheduledReport = null;

        _queryPlanQueries
            .Setup(q => q.GetAsync("facility-1", Frequency.Monthly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlan("Condition"));

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        _queryPlanQueries.Verify(q => q.GetAsync("facility-1", Frequency.Monthly, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckDependenciesAsync_NoReportableEventOrScheduledReport_ReturnsMet()
    {
        var log = CreateLog(logResourceTypes: ResourceType.Condition);
        log.ReportableEvent = null;
        log.ScheduledReport = null;

        var result = await CreateChecker().CheckDependenciesAsync(log, CancellationToken.None);

        Assert.True(result.AreDependenciesMet);
        _queryPlanQueries.Verify(q => q.GetAsync(It.IsAny<string>(), It.IsAny<Frequency>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
