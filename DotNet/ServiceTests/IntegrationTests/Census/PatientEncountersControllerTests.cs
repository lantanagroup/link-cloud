using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Controllers;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

[Collection("CensusIntegrationTests")]
public class PatientEncountersControllerTests : IClassFixture<CensusIntegrationTestFixture>
{
    private readonly CensusIntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PatientEncountersControllerTests(CensusIntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    #region GetCurrentPatientEncounters Paging Tests

    [Fact]
    public async Task GetCurrentPatientEncounters_WithPaging_ReturnsCorrectPage()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        // Create 25 encounters
        var encounters = new List<PatientEncounter>();
        for (int i = 0; i < 25; i++)
        {
            var correlationId = Guid.NewGuid().ToString();
            var encounter = new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = correlationId,
                MedicalRecordNumber = "MRN" + i,
                AdmitDate = DateTime.UtcNow.AddDays(-i),
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };
            encounters.Add(encounter);
        }

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act - Request page 2 with page size 10
            var result = await controller.GetCurrentPatientEncounters(
                facilityId: facilityId,
                correlationId: null,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 2,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

            Assert.Equal(2, pagedResult.Metadata.PageNumber);
            Assert.Equal(10, pagedResult.Metadata.PageSize);
            Assert.Equal(25, pagedResult.Metadata.TotalCount);
            Assert.Equal(3, pagedResult.Metadata.TotalPages); // 25 items / 10 per page = 3 pages
            Assert.Equal(10, pagedResult.Records.Count());
        }
        finally
        {
            // Cleanup
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetCurrentPatientEncounters_WithPaging_LastPageReturnsCorrectCount()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        // Create 25 encounters (so last page will have 5 items with page size 10)
        var encounters = new List<PatientEncounter>();
        for (int i = 0; i < 25; i++)
        {
            var correlationId = Guid.NewGuid().ToString();
            var encounter = new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = correlationId,
                MedicalRecordNumber = "MRN" + i,
                AdmitDate = DateTime.UtcNow.AddDays(-i),
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };
            encounters.Add(encounter);
        }

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act - Request page 3 (last page)
            var result = await controller.GetCurrentPatientEncounters(
                facilityId: facilityId,
                correlationId: null,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 3,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

            Assert.Equal(3, pagedResult.Metadata.PageNumber);
            Assert.Equal(5, pagedResult.Records.Count()); // Only 5 items on last page
            Assert.Equal(25, pagedResult.Metadata.TotalCount);
        }
        finally
        {
            // Cleanup
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetCurrentPatientEncounters_WithSortByAdmitDate_Ascending_ReturnsSortedResults()
    {
        // Arrange
        var facilityId = "SortTestFacility";

        var admitDates = new[]
        {
        DateTime.UtcNow.AddDays(-10), // oldest
        DateTime.UtcNow.AddDays(-5),
        DateTime.UtcNow.AddDays(-1)  // newest
    };

        var correlationIds = new List<string>();
        var encounters = new List<PatientEncounter>();

        foreach (var date in admitDates)
        {
            var corrId = Guid.NewGuid().ToString();
            correlationIds.Add(corrId);

            encounters.Add(new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = corrId,
                MedicalRecordNumber = "MRN",
                AdmitDate = date,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            });
        }

        await _fixture.DbContext.PatientEncounters.AddRangeAsync(encounters);
        await _fixture.DbContext.SaveChangesAsync();

        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();

        // Act
        var result = await controller.GetCurrentPatientEncounters(
            facilityId: facilityId,
            correlationId: null,
            sortBy: "AdmitDate",
            sortOrder: SortOrder.Ascending,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default
        );

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
        var records = paged.Records.ToList();

        Assert.Equal(3, records.Count);
        Assert.Equal(correlationIds[0], records[0].CorrelationId); // oldest
        Assert.Equal(correlationIds[1], records[1].CorrelationId);
        Assert.Equal(correlationIds[2], records[2].CorrelationId); // newest
    }

    [Fact]
    public async Task GetCurrentPatientEncounters_WithSortByAdmitDate_Descending_ReturnsSortedResults()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        // Create encounters with different admit dates and known correlation IDs for verification
        var encounters = new List<PatientEncounter>();
        var dates = new[] {
            DateTime.UtcNow.AddDays(-10), // Oldest
            DateTime.UtcNow.AddDays(-5),  // Middle
            DateTime.UtcNow.AddDays(-2)   // Newest, first in descending
        };

        var correlationIds = new List<string>();
        foreach (var date in dates)
        {
            var correlationId = Guid.NewGuid().ToString();
            var encounter = new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = correlationId,
                MedicalRecordNumber = Guid.NewGuid().ToString(),
                AdmitDate = date,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };
            encounters.Add(encounter);
            correlationIds.Add(correlationId);
        }

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetCurrentPatientEncounters(
                facilityId: facilityId,
                correlationId: null,
                sortBy: "AdmitDate",
                sortOrder: SortOrder.Descending,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

            var records = pagedResult.Records.ToList();
            Assert.Equal(3, records.Count);

            // Verify descending order: newest first
            Assert.Equal(correlationIds[2], records[0].CorrelationId); // Newest first
            Assert.Equal(correlationIds[1], records[1].CorrelationId);
            Assert.Equal(correlationIds[0], records[2].CorrelationId); // Oldest last

            _output.WriteLine($"Verified descending sort: {records[0].CorrelationId}, {records[1].CorrelationId}, {records[2].CorrelationId}");
        }
        finally
        {
            // Cleanup
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetCurrentPatientEncounters_WithCorrelationIdFilter_ReturnsOnlyMatchingEncounters()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "FilterTestFacility";

        var matchingCorrelationId = "match-123";
        var nonMatchingCorrelationId = "nomatch-456";

        var encounters = new[]
        {
            new PatientEncounter { FacilityId = facilityId, CorrelationId = matchingCorrelationId, AdmitDate = DateTime.UtcNow },
            new PatientEncounter { FacilityId = facilityId, CorrelationId = nonMatchingCorrelationId, AdmitDate = DateTime.UtcNow },
            new PatientEncounter { FacilityId = "OtherFacility", CorrelationId = matchingCorrelationId, AdmitDate = DateTime.UtcNow }
        };

        await _fixture.DbContext.PatientEncounters.AddRangeAsync(encounters);
        await _fixture.DbContext.SaveChangesAsync();


        // Act
        var result = await controller.GetCurrentPatientEncounters(
            facilityId: facilityId,
            correlationId: matchingCorrelationId,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default
        );

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

        Assert.Single(paged.Records);
        Assert.Equal(matchingCorrelationId, paged.Records.First().CorrelationId);
        Assert.Equal(1, paged.Metadata.TotalCount);
    }

    #endregion

    #region GetHistoricalMaterializedView Paging Tests

    [Fact]
    public async Task GetHistoricalMaterializedView_WithPaging_ReturnsCorrectPage()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;
        var facilityId = "TestFacility" + Guid.NewGuid();
        var threshold = DateTime.UtcNow;

        // Seed 25 ACTIVE encounters
        var encounters = new List<PatientEncounter>();
        for (int i = 0; i < 25; i++)
        {
            var encounter = new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = Guid.NewGuid().ToString(),
                MedicalRecordNumber = $"MRN{i}",
                AdmitDate = threshold.AddDays(-i - 1),
                DischargeDate = null, // Active
                CreateDate = threshold.AddDays(-i - 1),
                ModifyDate = threshold.AddDays(-i - 1)
            };
            encounters.Add(encounter);
        }
        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act - Request page 2 with page size 10
            var result = await controller.GetHistoricalMaterializedView(
                facilityId: facilityId,
                correlationId: null,
                dateThreshold: threshold,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 2,
                cancellationToken: default
            );

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Equal(2, pagedResult.Metadata.PageNumber);
            Assert.Equal(10, pagedResult.Metadata.PageSize);
            Assert.Equal(25, pagedResult.Metadata.TotalCount);
            Assert.Equal(3, pagedResult.Metadata.TotalPages);
            Assert.Equal(10, pagedResult.Records.Count());
        }
        finally
        {
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithPaging_LastPageReturnsCorrectCount()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;
        var facilityId = "PagingTest" + Guid.NewGuid();
        var threshold = DateTime.UtcNow;

        // Seed 24 ACTIVE encounters + 1 discharged
        var encounters = new List<PatientEncounter>();
        for (int i = 0; i < 24; i++)
        {
            var encounter = new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = Guid.NewGuid().ToString(),
                MedicalRecordNumber = $"MRN{i}",
                AdmitDate = threshold.AddDays(-i - 1),
                DischargeDate = null, // Active
                CreateDate = threshold.AddDays(-i - 1),
                ModifyDate = threshold.AddDays(-i - 1)
            };
            encounters.Add(encounter);
        }
        encounters.Add(new PatientEncounter
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = Guid.NewGuid().ToString(),
            MedicalRecordNumber = "MRN-Discharged",
            AdmitDate = threshold.AddDays(-10),
            DischargeDate = threshold.AddDays(-5), // Discharged before threshold
            CreateDate = threshold.AddDays(-10),
            ModifyDate = threshold.AddDays(-5)
        });

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act - Request page 3 (last page, 4 items)
            var result = await controller.GetHistoricalMaterializedView(
                facilityId: facilityId,
                correlationId: null,
                dateThreshold: threshold,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 3,
                cancellationToken: default
            );

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Equal(3, pagedResult.Metadata.PageNumber);
            Assert.Equal(10, pagedResult.Metadata.PageSize);
            Assert.Equal(24, pagedResult.Metadata.TotalCount);
            Assert.Equal(3, pagedResult.Metadata.TotalPages);
            Assert.Equal(4, pagedResult.Records.Count());
        }
        finally
        {
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithSortByModifyDate_Ascending_ReturnsSortedResults()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;
        var facilityId = "SortTest" + Guid.NewGuid();
        var threshold = DateTime.UtcNow;

        var dates = new[]
        {
                threshold.AddDays(-10), // Oldest
                threshold.AddDays(-5),
                threshold.AddDays(-2) // Newest
            };
        var correlationIds = new List<string>();
        var encounters = new List<PatientEncounter>();

        foreach (var date in dates)
        {
            var corrId = Guid.NewGuid().ToString();
            correlationIds.Add(corrId);
            encounters.Add(new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = corrId,
                MedicalRecordNumber = "MRN",
                AdmitDate = date,
                DischargeDate = null, // Active
                CreateDate = date,
                ModifyDate = date
            });
        }

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetHistoricalMaterializedView(
                facilityId: facilityId,
                correlationId: null,
                dateThreshold: threshold,
                sortBy: "ModifyDate",
                sortOrder: SortOrder.Ascending,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            var records = pagedResult.Records.ToList();
            Assert.Equal(3, records.Count);
            Assert.Equal(correlationIds[0], records[0].CorrelationId); // Oldest first
            Assert.Equal(correlationIds[1], records[1].CorrelationId);
            Assert.Equal(correlationIds[2], records[2].CorrelationId);
        }
        finally
        {
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithSortByModifyDate_Descending_ReturnsSortedResults()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;
        var facilityId = "SortTest" + Guid.NewGuid();
        var threshold = DateTime.UtcNow;

        var dates = new[]
        {
                threshold.AddDays(-10), // Oldest
                threshold.AddDays(-5),
                threshold.AddDays(-2) // Newest
            };
        var correlationIds = new List<string>();
        var encounters = new List<PatientEncounter>();

        foreach (var date in dates)
        {
            var corrId = Guid.NewGuid().ToString();
            correlationIds.Add(corrId);
            encounters.Add(new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = corrId,
                MedicalRecordNumber = "MRN",
                AdmitDate = date,
                DischargeDate = null, // Active
                CreateDate = date,
                ModifyDate = date
            });
        }

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetHistoricalMaterializedView(
                facilityId: facilityId,
                correlationId: null,
                dateThreshold: threshold,
                sortBy: "ModifyDate",
                sortOrder: SortOrder.Descending,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            var records = pagedResult.Records.ToList();
            Assert.Equal(3, records.Count);
            Assert.Equal(correlationIds[2], records[0].CorrelationId); // Newest first
            Assert.Equal(correlationIds[1], records[1].CorrelationId);
            Assert.Equal(correlationIds[0], records[2].CorrelationId);
        }
        finally
        {
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithCorrelationIdFilter_ReturnsOnlyMatching()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;
        var facilityId = "FilterTest" + Guid.NewGuid();
        var threshold = DateTime.UtcNow;
        var targetCorrelationId = Guid.NewGuid().ToString();
        var otherCorrelationId = Guid.NewGuid().ToString();

        var encounters = new[]
        {
                // Active, matching correlation
                new PatientEncounter
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    CorrelationId = targetCorrelationId,
                    MedicalRecordNumber = "MRN1",
                    AdmitDate = threshold.AddDays(-5),
                    DischargeDate = null,
                    CreateDate = threshold.AddDays(-5),
                    ModifyDate = threshold.AddDays(-5)
                },
                // Active, non-matching correlation
                new PatientEncounter
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    CorrelationId = otherCorrelationId,
                    MedicalRecordNumber = "MRN2",
                    AdmitDate = threshold.AddDays(-5),
                    DischargeDate = null,
                    CreateDate = threshold.AddDays(-5),
                    ModifyDate = threshold.AddDays(-5)
                },
                // Discharged before threshold, matching correlation
                new PatientEncounter
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    CorrelationId = targetCorrelationId,
                    MedicalRecordNumber = "MRN3",
                    AdmitDate = threshold.AddDays(-10),
                    DischargeDate = threshold.AddDays(-6),
                    CreateDate = threshold.AddDays(-10),
                    ModifyDate = threshold.AddDays(-6)
                }
            };

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetHistoricalMaterializedView(
                facilityId: facilityId,
                correlationId: targetCorrelationId,
                dateThreshold: threshold,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Single(pagedResult.Records);
            Assert.Equal(targetCorrelationId, pagedResult.Records.First().CorrelationId);
            Assert.Equal(1, pagedResult.Metadata.TotalCount);
        }
        finally
        {
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithDateThreshold_FiltersCorrectly()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;
        var facilityId = "ThresholdTest" + Guid.NewGuid();
        var threshold = DateTime.UtcNow.AddDays(-5);

        var encounters = new[]
        {
                // In range: Active (no discharge)
                new PatientEncounter
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    CorrelationId = Guid.NewGuid().ToString(),
                    MedicalRecordNumber = "MRN1",
                    AdmitDate = threshold.AddDays(-4),
                    DischargeDate = null,
                    CreateDate = threshold.AddDays(-4),
                    ModifyDate = threshold.AddDays(-4)
                },
                // In range: Active (discharge after threshold)
                new PatientEncounter
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    CorrelationId = Guid.NewGuid().ToString(),
                    MedicalRecordNumber = "MRN2",
                    AdmitDate = threshold.AddDays(-3),
                    DischargeDate = threshold.AddDays(1),
                    CreateDate = threshold.AddDays(-3),
                    ModifyDate = threshold.AddDays(-3)
                },
                // Out of range: Discharged before threshold
                new PatientEncounter
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    CorrelationId = Guid.NewGuid().ToString(),
                    MedicalRecordNumber = "MRN3",
                    AdmitDate = threshold.AddDays(-10),
                    DischargeDate = threshold.AddDays(-6),
                    CreateDate = threshold.AddDays(-10),
                    ModifyDate = threshold.AddDays(-6)
                },
                // Out of range: Admitted after threshold
                new PatientEncounter
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    CorrelationId = Guid.NewGuid().ToString(),
                    MedicalRecordNumber = "MRN4",
                    AdmitDate = threshold.AddDays(1),
                    DischargeDate = null,
                    CreateDate = threshold.AddDays(1),
                    ModifyDate = threshold.AddDays(1)
                }
            };

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetHistoricalMaterializedView(
                facilityId: facilityId,
                correlationId: null,
                dateThreshold: threshold,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Equal(2, pagedResult.Records.Count());
            var returnedCorrelationIds = pagedResult.Records.Select(e => e.CorrelationId).ToHashSet();
            var expectedCorrelationIds = encounters.Take(2).Select(e => e.CorrelationId).ToHashSet();
            Assert.Equal(expectedCorrelationIds, returnedCorrelationIds);
        }
        finally
        {
            db.PatientEncounters.RemoveRange(encounters);
            await db.SaveChangesAsync();
        }
    }

    #endregion
}