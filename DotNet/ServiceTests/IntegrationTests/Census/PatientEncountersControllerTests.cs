using LantanaGroup.Link.Census.Application.Models.Api;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Controllers;
using LantanaGroup.Link.Census.Domain.Context;
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

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var threshold = DateTime.UtcNow;

        // Create 25 events with different correlationIds
        var events = new List<PatientEvent>();
        for (int i = 0; i < 25; i++)
        {
            var correlationId = Guid.NewGuid().ToString();
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.ModifyDate = DateTime.UtcNow.AddDays(-i);
            events.Add(evt);
        }

        await db.PatientEvents.AddRangeAsync(events);
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
            Assert.NotNull(result);
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
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithPaging_LastPageReturnsCorrectCount()
    {
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "PagingTest";
        var threshold = DateTime.UtcNow.AddDays(-100);

        var encounters = Enumerable.Range(0, 24)
            .Select(i => new PatientEncounter
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                CorrelationId = Guid.NewGuid().ToString(),
                MedicalRecordNumber = $"MRN-{i}",
                AdmitDate = threshold.AddDays(i),
                CreateDate = DateTime.UtcNow
            }).ToList();

        await db.PatientEncounters.AddRangeAsync(encounters);
        await db.SaveChangesAsync();

        var result = await controller.GetHistoricalMaterializedView(
            facilityId, null, threshold, null, null, 10, 3, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(ok.Value);

        Assert.Equal(24, paged.Metadata.TotalCount);
        Assert.Equal(3, paged.Metadata.TotalPages);
        Assert.Equal(4, paged.Records.Count()); // Correct
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithSortByModifyDate_Ascending_ReturnsSortedResults()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var threshold = DateTime.UtcNow;

        // Create events with different modify dates
        var events = new List<PatientEvent>();
        var dates = new[] {
            DateTime.UtcNow.AddDays(-10), // Oldest
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow.AddDays(-2)   // Newest
        };

        var correlationIds = new List<string>();
        foreach (var date in dates)
        {
            var correlationId = Guid.NewGuid().ToString();
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.ModifyDate = date;
            evt.CreateDate = date;
            events.Add(evt);
            correlationIds.Add(correlationId);
        }

        await db.PatientEvents.AddRangeAsync(events);
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
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

            var records = pagedResult.Records.ToList();
            Assert.Equal(3, records.Count);

            Assert.Equal(correlationIds[0], records[0].CorrelationId); // Oldest first
            Assert.Equal(correlationIds[1], records[1].CorrelationId);
            Assert.Equal(correlationIds[2], records[2].CorrelationId);

            _output.WriteLine($"Verified ascending sort: {records[0].CorrelationId}, {records[1].CorrelationId}, {records[2].CorrelationId}");
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithSortByModifyDate_Descending_ReturnsSortedResults()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var threshold = DateTime.UtcNow;

        // Create events with different modify dates
        var events = new List<PatientEvent>();
        var dates = new[] {
            DateTime.UtcNow.AddDays(-10), // Oldest
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow.AddDays(-2)   // Newest, first in descending
        };

        var correlationIds = new List<string>();
        foreach (var date in dates)
        {
            var correlationId = Guid.NewGuid().ToString();
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.ModifyDate = date;
            evt.CreateDate = date;
            events.Add(evt);
            correlationIds.Add(correlationId);
        }

        await db.PatientEvents.AddRangeAsync(events);
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
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

            var records = pagedResult.Records.ToList();
            Assert.Equal(3, records.Count);

            Assert.Equal(correlationIds[2], records[0].CorrelationId); // Newest first
            Assert.Equal(correlationIds[1], records[1].CorrelationId);
            Assert.Equal(correlationIds[0], records[2].CorrelationId);

            _output.WriteLine($"Verified descending sort: {records[0].CorrelationId}, {records[1].CorrelationId}, {records[2].CorrelationId}");
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithCorrelationIdFilter_ReturnsOnlyMatching()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var threshold = DateTime.UtcNow;
        var targetCorrelationId = Guid.NewGuid().ToString();
        var otherCorrelationId = Guid.NewGuid().ToString();

        // Create events for target correlation (multiple to test latest)
        var events = new List<PatientEvent>();
        for (int i = 0; i < 3; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, targetCorrelationId);
            evt.ModifyDate = DateTime.UtcNow.AddDays(-i);
            events.Add(evt);
        }

        // Events for other correlation
        for (int i = 0; i < 2; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, otherCorrelationId);
            evt.ModifyDate = DateTime.UtcNow.AddDays(-i);
            events.Add(evt);
        }

        await db.PatientEvents.AddRangeAsync(events);
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
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

            Assert.Equal(1, pagedResult.Records.Count()); // Only one per correlation
            Assert.Equal(targetCorrelationId, pagedResult.Records.First().CorrelationId);
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetHistoricalMaterializedView_WithDateThreshold_FiltersCorrectly()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEncountersController>();
        var db = _fixture.DbContext;

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var threshold = DateTime.UtcNow.AddDays(-5); // Threshold 5 days ago

        // Create events with modify dates before and after threshold
        var eventsInRange = new List<PatientEvent>();
        var eventsOutsideRange = new List<PatientEvent>();

        // In range (<= threshold)
        for (int i = 6; i <= 9; i++) // 6 to 9 days ago
        {
            var correlationId = Guid.NewGuid().ToString();
            var date = DateTime.UtcNow.AddDays(-i);
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.ModifyDate = date;
            evt.CreateDate = date;
            eventsInRange.Add(evt);
        }

        // Outside range (> threshold, more recent)
        for (int i = 1; i <= 2; i++) // 1 to 2 days ago
        {
            var correlationId = Guid.NewGuid().ToString();
            var date = DateTime.UtcNow.AddDays(-i);
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.ModifyDate = date;
            evt.CreateDate = date;
            eventsOutsideRange.Add(evt);
        }

        var allEvents = eventsInRange.Concat(eventsOutsideRange).ToList();
        await db.PatientEvents.AddRangeAsync(allEvents);
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
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);

            Assert.Equal(4, pagedResult.Records.Count()); // 4 in range

            var returnedCorrelationIds = pagedResult.Records.Select(e => e.CorrelationId).ToHashSet();
            var expectedCorrelationIds = eventsInRange.Select(e => e.CorrelationId).ToHashSet();

            Assert.Equal(expectedCorrelationIds, returnedCorrelationIds);
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(allEvents);
            await db.SaveChangesAsync();
        }
    }

    #endregion
}