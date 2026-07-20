using System.Linq.Expressions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class LocationResolutionValidatorTests
{
    private const string FacilityId = "fac-1";

    private readonly Mock<IQueryPlanQueries> _queryPlanQueries = new();
    private readonly Mock<IOrganizationLocationConfigurationQueries> _locationConfigurationQueries = new();

    private LocationResolutionValidator CreateValidator() =>
        new(_queryPlanQueries.Object, _locationConfigurationQueries.Object);

    private static QueryPlanModel Plan(Frequency type, params string[] initialResourceTypes)
    {
        var initialQueries = new Dictionary<string, IQueryConfig>();
        for (int i = 0; i < initialResourceTypes.Length; i++)
        {
            initialQueries[i.ToString()] = new ParameterQueryConfig { ResourceType = initialResourceTypes[i] };
        }

        return new QueryPlanModel
        {
            FacilityId = FacilityId,
            Type = type,
            InitialQueries = initialQueries
        };
    }

    private void SetupPlans(params QueryPlanModel[] plans)
    {
        _queryPlanQueries
            .Setup(x => x.FindAsync(It.IsAny<Expression<Func<QueryPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans.ToList());
    }

    private void SetupActive(bool isActive)
    {
        _locationConfigurationQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isActive);
    }

    #region ValidateActivationAsync

    [Fact]
    public async Task ValidateActivation_AllPlansCompliant_DoesNotThrow()
    {
        SetupPlans(
            Plan(Frequency.Daily, "Patient", "Encounter", "Location"),
            Plan(Frequency.Weekly, "Encounter", "Location"));

        var validator = CreateValidator();

        await validator.ValidateActivationAsync(FacilityId);
    }

    [Fact]
    public async Task ValidateActivation_NoPlans_DoesNotThrow()
    {
        SetupPlans();

        var validator = CreateValidator();

        await validator.ValidateActivationAsync(FacilityId);
    }

    [Fact]
    public async Task ValidateActivation_PlanMissingEncounter_ThrowsNamingEncounterAndFrequency()
    {
        SetupPlans(Plan(Frequency.Daily, "Patient", "Location"));

        var validator = CreateValidator();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => validator.ValidateActivationAsync(FacilityId));
        Assert.Equal(
            "Cannot enable location resolution: the Daily query plan's initial queries must include an Encounter query.",
            ex.Message);
    }

    [Fact]
    public async Task ValidateActivation_PlanMissingLocation_ThrowsNamingLocationAndFrequency()
    {
        SetupPlans(Plan(Frequency.Weekly, "Encounter"));

        var validator = CreateValidator();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => validator.ValidateActivationAsync(FacilityId));
        Assert.Equal(
            "Cannot enable location resolution: the Weekly query plan's initial queries must include a Location query.",
            ex.Message);
    }

    [Fact]
    public async Task ValidateActivation_PlanMissingBoth_ThrowsNamingBoth()
    {
        SetupPlans(Plan(Frequency.Monthly, "Patient"));

        var validator = CreateValidator();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => validator.ValidateActivationAsync(FacilityId));
        Assert.Equal(
            "Cannot enable location resolution: the Monthly query plan's initial queries must include both an Encounter and a Location query.",
            ex.Message);
    }

    [Fact]
    public async Task ValidateActivation_MultipleNonCompliantPlans_ListsOffendingFrequencies()
    {
        SetupPlans(
            Plan(Frequency.Daily, "Encounter"),           // missing Location
            Plan(Frequency.Weekly, "Encounter", "Location"), // compliant
            Plan(Frequency.Monthly, "Patient"));          // missing both

        var validator = CreateValidator();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => validator.ValidateActivationAsync(FacilityId));
        Assert.Equal(
            "Cannot enable location resolution: the Daily and Monthly query plans are missing required " +
            "Encounter/Location queries in their initial queries.",
            ex.Message);
    }

    #endregion

    #region ValidateQueryPlanSaveAsync

    [Fact]
    public async Task ValidateQueryPlanSave_NotActive_DoesNotThrow()
    {
        SetupActive(false);

        var validator = CreateValidator();

        // Missing both Encounter and Location, but inactive => allowed.
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig { ResourceType = "Patient" }
        };

        await validator.ValidateQueryPlanSaveAsync(FacilityId, Frequency.Daily, initialQueries);
    }

    [Fact]
    public async Task ValidateQueryPlanSave_ActiveWithEncounterAndLocation_DoesNotThrow()
    {
        SetupActive(true);

        var validator = CreateValidator();

        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig { ResourceType = "Encounter" },
            ["1"] = new ReferenceQueryConfig { ResourceType = "Location" }
        };

        await validator.ValidateQueryPlanSaveAsync(FacilityId, Frequency.Daily, initialQueries);
    }

    [Fact]
    public async Task ValidateQueryPlanSave_ActiveMissingEncounter_Throws()
    {
        SetupActive(true);

        var validator = CreateValidator();

        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig { ResourceType = "Location" }
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => validator.ValidateQueryPlanSaveAsync(FacilityId, Frequency.Daily, initialQueries));
        Assert.Equal(
            "Cannot save this query plan: an Encounter query is required in the initial queries " +
            "while location resolution (parent organization) is active.",
            ex.Message);
    }

    [Fact]
    public async Task ValidateQueryPlanSave_ActiveMissingLocation_Throws()
    {
        SetupActive(true);

        var validator = CreateValidator();

        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig { ResourceType = "Encounter" }
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => validator.ValidateQueryPlanSaveAsync(FacilityId, Frequency.Daily, initialQueries));
        Assert.Equal(
            "Cannot save this query plan: a Location query is required in the initial queries " +
            "while location resolution (parent organization) is active.",
            ex.Message);
    }

    [Fact]
    public async Task ValidateQueryPlanSave_ActiveMissingBoth_ThrowsNamingBoth()
    {
        SetupActive(true);

        var validator = CreateValidator();

        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig { ResourceType = "Patient" }
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => validator.ValidateQueryPlanSaveAsync(FacilityId, Frequency.Daily, initialQueries));
        Assert.Equal(
            "Cannot save this query plan: both an Encounter and a Location query are required in the initial queries " +
            "while location resolution (parent organization) is active.",
            ex.Message);
    }

    #endregion
}
