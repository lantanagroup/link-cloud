using IntegrationTests.Census;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.IntegrationTests.Census.Queries;

[Collection("CensusIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class PatientEncounterQueriesTests : IClassFixture<CensusIntegrationTestFixture>
{
    private readonly CensusIntegrationTestFixture _fixture;

    public PatientEncounterQueriesTests(CensusIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEncounter_WhenExists()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CensusContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var facilityId = "TestFacility";
        var testEncounter = new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = Guid.NewGuid().ToString(),
            AdmitDate = DateTime.UtcNow.AddDays(-1),
            DischargeDate = null,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow,
            PatientIdentifiers = new List<PatientIdentifier>
            {
                new PatientIdentifier { Id = Guid.NewGuid(), Identifier = "ID1", SourceType = SourceType.FHIR, CreateDate = DateTime.UtcNow }
            },
            PatientVisitIdentifiers = new List<PatientVisitIdentifier>
            {
                new PatientVisitIdentifier { Id = Guid.NewGuid(), Identifier = "VID1", SourceType = SourceType.FHIR, CreateDate = DateTime.UtcNow }
            }
        };
        dbContext.PatientEncounters.Add(testEncounter);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        // Act
        var result = await queries.GetByIdAsync(testEncounter.Id, facilityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testEncounter.Id, result.Id);
        Assert.Equal(testEncounter.CorrelationId, result.CorrelationId);
        Assert.Equal(1, result.PatientIdentifiers.Count);
        Assert.Equal(1, result.PatientVisitIdentifiers.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CensusContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        // Act
        var result = await queries.GetByIdAsync(Guid.NewGuid(), "NonExistentFacility");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsPagedResults_WithCorrelationId()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CensusContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var facilityId = "TestFacility";
        var correlationId = Guid.NewGuid().ToString();

        // Add matching encounter
        var matchingEncounter = new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            AdmitDate = DateTime.UtcNow.AddDays(-1),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };
        dbContext.PatientEncounters.Add(matchingEncounter);

        // Add non-matching encounter
        var nonMatchingEncounter = new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = Guid.NewGuid().ToString(),
            AdmitDate = DateTime.UtcNow.AddDays(-2),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };
        dbContext.PatientEncounters.Add(nonMatchingEncounter);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        // Act
        var result = await queries.SearchAsync(new SearchPatientEncounterModel
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            PageSize = 10,
            PageNumber = 1
        });

        // Assert
        Assert.Equal(1, result.Records.Count);
        Assert.Equal(1, result.Metadata.TotalCount);
        Assert.Equal(matchingEncounter.Id, result.Records.First().Id);
    }

    [Fact]
    public async Task SearchAsync_ReturnsCurrentEncounters_WithThreshold()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CensusContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var facilityId = "TestFacility";
        var threshold = DateTime.UtcNow;

        // Add current encounter (admitted before threshold, no discharge or discharge after)
        var currentEncounter = new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = Guid.NewGuid().ToString(),
            AdmitDate = threshold.AddDays(-1),
            DischargeDate = null,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };
        dbContext.PatientEncounters.Add(currentEncounter);

        // Add discharged before threshold
        var dischargedEncounter = new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = Guid.NewGuid().ToString(),
            AdmitDate = threshold.AddDays(-2),
            DischargeDate = threshold.AddDays(-1),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };
        dbContext.PatientEncounters.Add(dischargedEncounter);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        // Act
        var result = await queries.SearchAsync(new SearchPatientEncounterModel
        {
            FacilityId = facilityId,
            Threshold = threshold,
            PageSize = 10,
            PageNumber = 1
        });

        // Assert
        Assert.Equal(1, result.Records.Count);
        Assert.Equal(currentEncounter.Id, result.Records.First().Id);
    }

    [Fact]
    public async Task SearchAsync_AppliesSortingAndPagination()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CensusContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var facilityId = "TestFacility";

        // Add encounters with different admit dates
        dbContext.PatientEncounters.Add(new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = "Corr1",
            AdmitDate = DateTime.UtcNow.AddDays(-3),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        });

        dbContext.PatientEncounters.Add(new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = "Corr2",
            AdmitDate = DateTime.UtcNow.AddDays(-1),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        });

        dbContext.PatientEncounters.Add(new PatientEncounter
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            CorrelationId = "Corr3",
            AdmitDate = DateTime.UtcNow.AddDays(-2),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        // Act - Sort descending by admit date, page 1 size 2
        var result = await queries.SearchAsync(new SearchPatientEncounterModel
        {
            FacilityId = facilityId,
            SortBy = "AdmitDate",
            SortOrder = SortOrder.Descending,
            PageSize = 2,
            PageNumber = 1
        });

        // Assert
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(3, result.Metadata.TotalCount);
        Assert.True(result.Records[0].AdmitDate > result.Records[1].AdmitDate);
    }
}