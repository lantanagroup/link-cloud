using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
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

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class QueryPlanManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
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
        return new QueryPlanManager(database, logger);
    }

    [Fact]
    public async Task AddAsync_ValidModel_ReturnsQueryPlanModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var model = CreateValidCreateQueryPlanModel();

        // Act
        var result = await manager.AddAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestPlan", result.PlanName);
        Assert.Equal("TestFacility", result.FacilityId);
        Assert.Equal(Frequency.Daily, result.Type);
        Assert.Equal("TestEHR", result.EHRDescription);
        Assert.Equal("1d", result.LookBack);
        Assert.NotNull(result.CreateDate);
        Assert.NotNull(result.ModifyDate);
        Assert.Equal(result.CreateDate, result.ModifyDate);
        Assert.Equal(1, result.InitialQueries.Count);
        Assert.Equal(1, result.SupplementalQueries.Count);

        // Verify database
        var savedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == "TestFacility" && q.Type == Frequency.Daily);
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
        var model = CreateInvalidOrderCreateQueryPlanModel(initialInvalid: true, supplementalInvalid: false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<IncorrectQueryPlanOrderException>(() => manager.AddAsync(model));
        Assert.Contains("InitialQueries", ex.Message);
    }

    [Fact]
    public async Task AddAsync_InvalidSupplementalQueryOrder_ThrowsIncorrectQueryPlanOrderException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateInvalidOrderCreateQueryPlanModel(initialInvalid: false, supplementalInvalid: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<IncorrectQueryPlanOrderException>(() => manager.AddAsync(model));
        Assert.Contains("SupplementalQueries", ex.Message);
    }

    [Fact]
    public async Task AddAsync_AllParameterQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateAllParameterCreateQueryPlanModel();

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
        var model = CreateAllReferenceCreateQueryPlanModel();

        // Act
        var result = await manager.AddAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InitialQueries.Count);
        Assert.All(result.InitialQueries.Values, q => Assert.IsType<ReferenceQueryConfig>(q));
    }

    [Fact]
    public async Task AddAsync_EmptyQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateValidCreateQueryPlanModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>();
        model.SupplementalQueries = new Dictionary<string, IQueryConfig>();

        // Act
        var result = await manager.AddAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.InitialQueries);
        Assert.Empty(result.SupplementalQueries);
    }

    [Fact]
    public async Task AddAsync_NullInitialQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateValidCreateQueryPlanModel();
        model.InitialQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.AddAsync(model));
        Assert.Contains("NOT NULL constraint failed: queryPlan.InitialQueries", ex.InnerException.Message);
    }

    [Fact]
    public async Task AddAsync_NullSupplementalQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateValidCreateQueryPlanModel();
        model.SupplementalQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.AddAsync(model));
        Assert.Contains("NOT NULL constraint failed: queryPlan.SupplementalQueries", ex.InnerException.Message);
    }

    [Fact]
    public async Task UpdateAsync_ValidModel_ReturnsUpdatedQueryPlanModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
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

        var created = await queries.GetAsync(existing.FacilityId, Frequency.Daily);

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("UpdatedPlan", result.PlanName);
        Assert.Equal("UpdatedEHR", result.EHRDescription);
        Assert.Equal("2d", result.LookBack);
        Assert.Equal(created.CreateDate, result.CreateDate);
        Assert.True(result.ModifyDate > created.ModifyDate);
        Assert.Equal(1, result.InitialQueries.Count);
        Assert.Equal(1, result.SupplementalQueries.Count);

        // Verify database
        var updatedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == "TestFacility" && q.Type == Frequency.Daily);
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
        var model = CreateInvalidOrderUpdateQueryPlanModel(initialInvalid: true, supplementalInvalid: false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<IncorrectQueryPlanOrderException>(() => manager.UpdateAsync(model));
        Assert.Contains("InitialQueries", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_InvalidSupplementalQueryOrder_ThrowsIncorrectQueryPlanOrderException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = CreateInvalidOrderUpdateQueryPlanModel(initialInvalid: false, supplementalInvalid: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<IncorrectQueryPlanOrderException>(() => manager.UpdateAsync(model));
        Assert.Contains("SupplementalQueries", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_AllParameterQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateAllParameterUpdateQueryPlanModel();

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateAllReferenceUpdateQueryPlanModel();

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InitialQueries.Count);
        Assert.All(result.InitialQueries.Values, q => Assert.IsType<ReferenceQueryConfig>(q));
    }

    [Fact]
    public async Task UpdateAsync_EmptyQueries_Valid()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig> { { "old", new ParameterQueryConfig() } },
            SupplementalQueries = new Dictionary<string, IQueryConfig> { { "old", new ParameterQueryConfig() } }
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>();
        model.SupplementalQueries = new Dictionary<string, IQueryConfig>();

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.InitialQueries);
        Assert.Empty(result.SupplementalQueries);
    }

    [Fact]
    public async Task UpdateAsync_NullInitialQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();
        model.InitialQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("NOT NULL constraint failed: queryPlan.InitialQueries", ex.InnerException.Message);
    }

    [Fact]
    public async Task UpdateAsync_NullSupplementalQueries_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();
        model.SupplementalQueries = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("NOT NULL constraint failed: queryPlan.SupplementalQueries", ex.InnerException.Message);
    }

    [Fact]
    public async Task UpdateAsync_NullPlanName_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();
        model.PlanName = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("NOT NULL constraint failed: queryPlan.PlanName", ex.InnerException.Message);
    }

    [Fact]
    public async Task UpdateAsync_NullEHRDescription_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();
        model.EHRDescription = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("NOT NULL constraint failed: queryPlan.EHRDescription", ex.InnerException.Message);
    }

    [Fact]
    public async Task UpdateAsync_NullLookBack_ThrowsDbUpdateException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = CreateValidUpdateQueryPlanModel();
        model.LookBack = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(model));
        Assert.Contains("NOT NULL constraint failed: queryPlan.LookBack", ex.InnerException.Message);
    }

    [Fact]
    public async Task DeleteAsync_ExistingQueryPlan_DeletesSuccessfully()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed query plan
        var queryPlan = new QueryPlan
        {
            FacilityId = "TestFacility",
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
        await manager.DeleteAsync("TestFacility", Frequency.Daily);

        // Assert
        var deletedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == "TestFacility" && q.Type == Frequency.Daily);
        Assert.Null(deletedPlan);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAsync("NonExisting", Frequency.Daily));
    }

    private CreateQueryPlanModel CreateValidCreateQueryPlanModel()
    {
        return new CreateQueryPlanModel
        {
            PlanName = "TestPlan",
            FacilityId = "TestFacility",
            EHRDescription = "TestEHR",
            LookBack = "1d",
            Type = Frequency.Daily,
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

    private CreateQueryPlanModel CreateInvalidOrderCreateQueryPlanModel(bool initialInvalid, bool supplementalInvalid)
    {
        var model = CreateValidCreateQueryPlanModel();
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

    private CreateQueryPlanModel CreateAllParameterCreateQueryPlanModel()
    {
        var model = CreateValidCreateQueryPlanModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            { "1", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } },
            { "2", new ParameterQueryConfig { ResourceType = "Encounter", Parameters = new List<IParameter> { new ResourceIdsParameter { Name = "patient", Resource = "Patient" } } } }
        };
        return model;
    }

    private CreateQueryPlanModel CreateAllReferenceCreateQueryPlanModel()
    {
        var model = CreateValidCreateQueryPlanModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            { "1", new ReferenceQueryConfig { ResourceType = "Patient" } },
            { "2", new ReferenceQueryConfig { ResourceType = "Encounter" } }
        };
        return model;
    }

    private UpdateQueryPlanModel CreateValidUpdateQueryPlanModel()
    {
        return new UpdateQueryPlanModel
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
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

    private UpdateQueryPlanModel CreateInvalidOrderUpdateQueryPlanModel(bool initialInvalid, bool supplementalInvalid)
    {
        var model = CreateValidUpdateQueryPlanModel();
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

    private UpdateQueryPlanModel CreateAllParameterUpdateQueryPlanModel()
    {
        var model = CreateValidUpdateQueryPlanModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            { "1", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } },
            { "2", new ParameterQueryConfig { ResourceType = "Encounter", Parameters = new List<IParameter> { new ResourceIdsParameter { Name = "patient", Resource = "Patient" } } } }
        };
        return model;
    }

    private UpdateQueryPlanModel CreateAllReferenceUpdateQueryPlanModel()
    {
        var model = CreateValidUpdateQueryPlanModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            { "1", new ReferenceQueryConfig { ResourceType = "Patient" } },
            { "2", new ReferenceQueryConfig { ResourceType = "Encounter" } }
        };
        return model;
    }
}