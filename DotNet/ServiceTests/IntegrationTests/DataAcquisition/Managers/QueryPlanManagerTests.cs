using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class QueryPlanManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public QueryPlanManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IQueryPlanManager CreateManager(IServiceScope scope)
    {
        var logger = new Mock<ILogger<QueryPlanManager>>().Object;
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        IQueryPlanValidator validator = new Mock<QueryPlanValidator>().Object;
        var locationResolutionValidator = scope.ServiceProvider.GetRequiredService<ILocationResolutionValidator>();
        return new QueryPlanManager(database, logger, validator, locationResolutionValidator);
    }

    private static string CreateFacilityId() => $"NonExistent_{Guid.NewGuid():N}";

    [Fact]
    public async Task AddAsync_ValidModel_ReturnsQueryPlanModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var manager = CreateManager(scope);
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;
        var model = CreateValidCreateQueryPlanModel(facilityId, frequency);

        // Act
        var result = await manager.AddAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestPlan", result.PlanName);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal(frequency, result.Type);
        Assert.Equal("TestEHR", result.EHRDescription);
        Assert.Equal("1d", result.LookBack);
        Assert.NotNull(result.ModifyDate);
        Assert.Equal(result.CreateDate, result.ModifyDate);
        Assert.Single(result.InitialQueries);
        Assert.Single(result.SupplementalQueries);

        // Verify database
        var savedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == frequency);
        Assert.NotNull(savedPlan);
        Assert.Equal("TestPlan", savedPlan.PlanName);
    }

    [Fact]
    public async Task AddAsync_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.AddAsync(null));
    }

    [Fact]
    public async Task AddAsync_InvalidInitialQueryOrder_ThrowsIncorrectQueryPlanOrderException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateInvalidOrderCreateQueryPlanModel(CreateFacilityId(), Frequency.Daily, initialInvalid: true, supplementalInvalid: false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.AddAsync(model));
        Assert.Contains("InitialQueries", ex.Message);
    }

    [Fact]
    public async Task AddAsync_InvalidSupplementalQueryOrder_ThrowsIncorrectQueryPlanOrderException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateInvalidOrderCreateQueryPlanModel(CreateFacilityId(), Frequency.Daily, initialInvalid: false, supplementalInvalid: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.AddAsync(model));
        Assert.Contains("SupplementalQueries", ex.Message);
    }

    [Fact]
    public async Task AddAsync_AllParameterQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateAllParameterCreateQueryPlanModel(CreateFacilityId(), Frequency.Daily);

        // Act
        var result = await manager.AddAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InitialQueries.Count);
        Assert.All(result.InitialQueries.Values, q => Assert.IsType<ParameterQueryConfig>(q));
    }

    [Fact]
    public async Task AddAsync_AllReferenceQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateAllReferenceCreateQueryPlanModel(CreateFacilityId(), Frequency.Daily);

        // Act
        var result = await manager.AddAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InitialQueries.Count);
        Assert.All(result.InitialQueries.Values, q => Assert.IsType<ReferenceQueryConfig>(q));
    }

    [Fact]
    public async Task AddAsync_EmptyInitialAndSupplementalQueries_ThrowsBadRequestException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateValidCreateQueryPlanModel(CreateFacilityId(), Frequency.Daily);
        model.InitialQueries = new Dictionary<string, IQueryConfig>();
        model.SupplementalQueries = new Dictionary<string, IQueryConfig>();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.AddAsync(model));

        Assert.Contains("InitialQueries cannot be null or empty", ex.Message);
        Assert.Contains("SupplementalQueries cannot be null or empty", ex.Message);
    }

    [Fact]
    public async Task AddAsync_NullInitialQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateValidCreateQueryPlanModel(CreateFacilityId(), Frequency.Daily);
        model.InitialQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.AddAsync(model));
    }

    [Fact]
    public async Task AddAsync_NullSupplementalQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateValidCreateQueryPlanModel(CreateFacilityId(), Frequency.Daily);
        model.SupplementalQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.AddAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_ValidModel_ReturnsUpdatedQueryPlanModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "OldPlan",
            EHRDescription = "OldEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>(),
            CreateDate = DateTime.UtcNow.AddDays(-1),
            ModifyDate = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var created = await queries.GetAsync(existing.FacilityId, frequency);

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("UpdatedPlan", result.PlanName);
        Assert.Equal("UpdatedEHR", result.EHRDescription);
        Assert.Equal("2d", result.LookBack);
        Assert.Equal(created.CreateDate, result.CreateDate);
        Assert.True(result.ModifyDate > created.ModifyDate);
        Assert.Single(result.InitialQueries);
        Assert.Single(result.SupplementalQueries);

        // Verify database
        var updatedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == frequency);
        Assert.NotNull(updatedPlan);
        Assert.Equal("UpdatedPlan", updatedPlan.PlanName);
    }

    [Fact]
    public async Task UpdateAsync_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.UpdateAsync(null));
    }

    [Fact]
    public async Task UpdateAsync_InvalidInitialQueryOrder_ThrowsIncorrectQueryPlanOrderException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateInvalidOrderUpdateQueryPlanModel(CreateFacilityId(), Frequency.Daily, initialInvalid: true, supplementalInvalid: false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.UpdateAsync(model));
        Assert.Contains("InitialQueries", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_InvalidSupplementalQueryOrder_ThrowsIncorrectQueryPlanOrderException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateInvalidOrderUpdateQueryPlanModel(CreateFacilityId(), Frequency.Daily, initialInvalid: false, supplementalInvalid: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.UpdateAsync(model));
        Assert.Contains("SupplementalQueries", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(CreateFacilityId(), Frequency.Daily);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_AllParameterQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateAllParameterUpdateQueryPlanModel(facilityId, frequency);

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InitialQueries.Count);
        Assert.All(result.InitialQueries.Values, q => Assert.IsType<ParameterQueryConfig>(q));
    }

    [Fact]
    public async Task UpdateAsync_AllReferenceQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateAllReferenceUpdateQueryPlanModel(facilityId, frequency);

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InitialQueries.Count);
        Assert.All(result.InitialQueries.Values, q => Assert.IsType<ReferenceQueryConfig>(q));
    }

    [Fact]
    public async Task UpdateAsync_EmptyQueries_Invalid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig> { { "old", new ParameterQueryConfig() } },
            SupplementalQueries = new Dictionary<string, IQueryConfig> { { "old", new ParameterQueryConfig() } }
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.InitialQueries = new Dictionary<string, IQueryConfig>();
        model.SupplementalQueries = new Dictionary<string, IQueryConfig>();

        // Act
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_NullInitialQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.InitialQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_NullSupplementalQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.SupplementalQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_NullPlanName_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.PlanName = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("Cannot insert the value NULL into column", ex.InnerException!.Message);
    }

    [Fact]
    public async Task UpdateAsync_NullEHRDescription_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.EHRDescription = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("Cannot insert the value NULL into column", ex.InnerException!.Message);
    }

    [Fact]
    public async Task UpdateAsync_NullLookBack_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = CreateFacilityId();
        var frequency = Frequency.Daily;


        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.LookBack = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("Cannot insert the value NULL into column", ex.InnerException!.Message);
    }

    [Fact]
    public async Task DeleteAsync_ExistingQueryPlan_DeletesSuccessfully()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        // Use a unique facility for isolation
        var facilityId = $"DeleteTest_{Guid.NewGuid():N}";

        // Seed query plan
        var queryPlan = new QueryPlan
        {
            FacilityId = facilityId,
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(queryPlan);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        await manager.DeleteAsync(facilityId, Frequency.Daily);

        // Assert
        var deletedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == Frequency.Daily);
        Assert.Null(deletedPlan);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAsync("NonExisting", Frequency.Daily));
    }

    private CreateQueryPlanModel CreateValidCreateQueryPlanModel(string facilityId, Frequency frequency)
    {
        return new CreateQueryPlanModel
        {
            PlanName = "TestPlan",
            FacilityId = facilityId,
            EHRDescription = "TestEHR",
            LookBack = "1d",
            Type = frequency,
            InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } }
            },
            SupplementalQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Encounter" } }
            }
        };
    }

    private CreateQueryPlanModel CreateInvalidOrderCreateQueryPlanModel(string facilityId, Frequency frequency, bool initialInvalid, bool supplementalInvalid)
    {
        var model = CreateValidCreateQueryPlanModel(facilityId, frequency);
        if (initialInvalid)
        {
            model.InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Encounter" } },
                { "2", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } }
            };
        }
        if (supplementalInvalid)
        {
            model.SupplementalQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Condition" } },
                { "2", new ParameterQueryConfig { ResourceType = "Observation", Parameters = new List<IParameter> { new VariableParameter { Name = "date", Variable = Variable.PeriodStart } } } }
            };
        }
        return model;
    }

    private CreateQueryPlanModel CreateAllParameterCreateQueryPlanModel(string facilityId, Frequency frequency)
    {
        var model = CreateValidCreateQueryPlanModel(facilityId, frequency);
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            {
                "1",
                new ParameterQueryConfig
                {
                    ResourceType = "Patient",
                    Parameters = new List<IParameter>
                    {
                        new LiteralParameter
                        {
                            Name = "id",
                            Literal = "123"
                        }
                    }
                }
            },
            {
                "2",
                new ParameterQueryConfig
                {
                    ResourceType = "Encounter",
                    Parameters = new List<IParameter>
                    {
                        new ResourceIdsParameter
                        {
                            Name = "patient",
                            Resource = "Patient",
                            Paged = "50"
                        }
                    }
                }
            }
        };
        return model;
    }

    private CreateQueryPlanModel CreateAllReferenceCreateQueryPlanModel(string facilityId, Frequency frequency)
    {
        var model = CreateValidCreateQueryPlanModel(facilityId, frequency);
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            { "1", new ReferenceQueryConfig { ResourceType = "Patient" } },
            { "2", new ReferenceQueryConfig { ResourceType = "Encounter" } }
        };
        return model;
    }

    private UpdateQueryPlanModel CreateValidUpdateQueryPlanModel(string facilityId, Frequency frequency)
    {
        return new UpdateQueryPlanModel
        {
            FacilityId = facilityId,
            Type = frequency,
            PlanName = "UpdatedPlan",
            EHRDescription = "UpdatedEHR",
            LookBack = "2d",
            InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } }
            },
            SupplementalQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Encounter" } }
            }
        };
    }

    private UpdateQueryPlanModel CreateInvalidOrderUpdateQueryPlanModel(string facilityId, Frequency frequency, bool initialInvalid, bool supplementalInvalid)
    {
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        if (initialInvalid)
        {
            model.InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Encounter" } },
                { "2", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } }
            };
        }
        if (supplementalInvalid)
        {
            model.SupplementalQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Condition" } },
                { "2", new ParameterQueryConfig { ResourceType = "Observation", Parameters = new List<IParameter> { new VariableParameter { Name = "date", Variable = Variable.PeriodStart } } } }
            };
        }
        return model;
    }

    private UpdateQueryPlanModel CreateAllParameterUpdateQueryPlanModel(string facilityId, Frequency frequency)
    {
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            { "1", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } },
            { "2", new ParameterQueryConfig { ResourceType = "Encounter", Parameters = new List<IParameter> { new ResourceIdsParameter { Name = "patient", Resource = "Patient", Paged = "50" } } } }
        };
        return model;
    }

    private UpdateQueryPlanModel CreateAllReferenceUpdateQueryPlanModel(string facilityId, Frequency frequency)
    {
        var model = CreateValidUpdateQueryPlanModel(facilityId, frequency);
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            { "1", new ReferenceQueryConfig { ResourceType = "Patient" } },
            { "2", new ReferenceQueryConfig { ResourceType = "Encounter" } }
        };
        return model;
    }
}

