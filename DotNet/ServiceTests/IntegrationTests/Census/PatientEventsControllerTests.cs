using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Controllers;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

[Collection("CensusIntegrationTests")]
public class PatientEventsControllerTests : IClassFixture<CensusIntegrationTestFixture>
{
    private readonly CensusIntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PatientEventsControllerTests(CensusIntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    #region GetPatientEvents Paging Tests

    [Fact]
    public async Task GetPatientEvents_WithPaging_ReturnsCorrectPage()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();

        // Create 25 events
        var events = new List<PatientEvent>();
        for (int i = 0; i < 25; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            events.Add(evt);
        }

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        try
        {
            // Act - Request page 2 with page size 10
            var result = await controller.GetPatientEvents(
                facilityId: facilityId,
                correlationId: null,
                startDate: null,
                endDate: null,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 2,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEventModel>>(okResult.Value);

            Assert.Equal(2, pagedResult.Metadata.PageNumber);
            Assert.Equal(10, pagedResult.Metadata.PageSize);
            Assert.Equal(25, pagedResult.Metadata.TotalCount);
            Assert.Equal(3, pagedResult.Metadata.TotalPages); // 25 items / 10 per page = 3 pages
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
    public async Task GetPatientEvents_WithPaging_LastPageReturnsCorrectCount()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        // Create 25 events (so last page will have 5 items with page size 10)
        var events = new List<PatientEvent>();
        for (int i = 0; i < 25; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            events.Add(evt);
        }

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        try
        {
            // Act - Request page 3 (last page)
            var result = await controller.GetPatientEvents(
                facilityId: facilityId,
                correlationId: null,
                startDate: null,
                endDate: null,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 3,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEventModel>>(okResult.Value);

            Assert.Equal(3, pagedResult.Metadata.PageNumber);
            Assert.Equal(5, pagedResult.Records.Count()); // Only 5 items on last page
            Assert.Equal(25, pagedResult.Metadata.TotalCount);
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetPatientEvents_WithSortByCreateDate_Ascending_ReturnsSortedResults()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        // Create events with different dates and known IDs for verification
        var events = new List<PatientEvent>();
        var dates = new[] {
            DateTime.UtcNow.AddDays(-10), // Oldest
            DateTime.UtcNow.AddDays(-5),  // Middle
            DateTime.UtcNow.AddDays(-2)   // Newest
        };

        var eventIds = new List<string>();
        foreach (var date in dates)
        {
            var patientId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.CreateDate = date; // Explicitly set CreateDate
            events.Add(evt);
            eventIds.Add(evt.Id);
        }

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetPatientEvents(
                facilityId: facilityId,
                correlationId: null,
                startDate: null,
                endDate: null,
                sortBy: "CreateDate",
                sortOrder: SortOrder.Ascending,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEventModel>>(okResult.Value);

            var records = pagedResult.Records.ToList();
            Assert.Equal(3, records.Count);

            // Verify ascending order by checking the IDs match expected order
            // Since we created events in ascending date order, the IDs should match
            Assert.Equal(eventIds[0], records[0].Id); // Oldest first
            Assert.Equal(eventIds[1], records[1].Id); // Middle
            Assert.Equal(eventIds[2], records[2].Id); // Newest last

            _output.WriteLine($"Verified ascending sort: {records[0].Id}, {records[1].Id}, {records[2].Id}");
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetPatientEvents_WithSortByCreateDate_Descending_ReturnsSortedResults()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        // Create events with different dates and known IDs for verification
        var events = new List<PatientEvent>();
        var dates = new[] {
            DateTime.UtcNow.AddDays(-10), // Oldest
            DateTime.UtcNow.AddDays(-5),  // Middle
            DateTime.UtcNow.AddDays(-2)   // Newest
        };

        var eventIds = new List<string>();
        foreach (var date in dates)
        {
            var patientId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.CreateDate = date;
            events.Add(evt);
            eventIds.Add(evt.Id);
        }

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetPatientEvents(
                facilityId: facilityId,
                correlationId: null,
                startDate: null,
                endDate: null,
                sortBy: "CreateDate",
                sortOrder: SortOrder.Descending,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEventModel>>(okResult.Value);

            var records = pagedResult.Records.ToList();
            Assert.Equal(3, records.Count);

            // Verify descending order by checking the IDs match expected reverse order
            Assert.Equal(eventIds[2], records[0].Id); // Newest first
            Assert.Equal(eventIds[1], records[1].Id); // Middle
            Assert.Equal(eventIds[0], records[2].Id); // Oldest last

            _output.WriteLine($"Verified descending sort: {records[0].Id}, {records[1].Id}, {records[2].Id}");
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetPatientEvents_WithInvalidSortBy_UsesDefaultSorting()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        // Create a few events
        var events = new List<PatientEvent>();
        for (int i = 0; i < 3; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            events.Add(evt);
        }

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        try
        {
            // Act - Use invalid sort field
            var result = await controller.GetPatientEvents(
                facilityId: facilityId,
                correlationId: null,
                startDate: null,
                endDate: null,
                sortBy: "InvalidField",
                sortOrder: SortOrder.Ascending,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert - Should still return results (will default to Id sorting)
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEventModel>>(okResult.Value);

            Assert.Equal(3, pagedResult.Records.Count());
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetPatientEvents_WithNoResults_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();

        var nonExistentFacilityId = "NonExistent" + Guid.NewGuid().ToString();

        // Act
        var result = await controller.GetPatientEvents(
            facilityId: nonExistentFacilityId,
            correlationId: null,
            startDate: null,
            endDate: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default
        );

        // Assert
        Assert.NotNull(result);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPatientEvents_WithEmptyFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();

        // Act
        var result = await controller.GetPatientEvents(
            facilityId: "",
            correlationId: null,
            startDate: null,
            endDate: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default
        );

        // Assert
        Assert.NotNull(result);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPatientEvents_WithCorrelationIdFilter_ReturnsOnlyMatchingEvents()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var targetCorrelationId = Guid.NewGuid().ToString();
        var otherCorrelationId = Guid.NewGuid().ToString();

        // Create events with different correlation IDs
        var events = new List<PatientEvent>();

        // 3 events with target correlation ID
        for (int i = 0; i < 3; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, targetCorrelationId);
            events.Add(evt);
        }

        // 2 events with different correlation ID
        for (int i = 0; i < 2; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, otherCorrelationId);
            events.Add(evt);
        }

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetPatientEvents(
                facilityId: facilityId,
                correlationId: targetCorrelationId,
                startDate: null,
                endDate: null,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEventModel>>(okResult.Value);

            Assert.Equal(3, pagedResult.Records.Count());
            Assert.All(pagedResult.Records, evt => Assert.Equal(targetCorrelationId, evt.CorrelationId));
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(events);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetPatientEvents_WithDateRangeFilter_ReturnsOnlyEventsInRange()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow.AddDays(-10);
        var endDate = DateTime.UtcNow.AddDays(-5);

        // Create events with different dates
        var eventsInRange = new List<PatientEvent>();
        var eventsOutsideRange = new List<PatientEvent>();

        // Events within range (should be returned)
        for (int i = 6; i <= 9; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            var date = DateTime.UtcNow.AddDays(-i);
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.CreateDate = date;
            eventsInRange.Add(evt);
        }

        // Events outside range (should not be returned)
        for (int i = 1; i <= 2; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            var date = DateTime.UtcNow.AddDays(-i); // Too recent
            var payload = new FHIRListAdmitPayload(patientId, date);
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            evt.CreateDate = date;
            eventsOutsideRange.Add(evt);
        }

        var allEvents = eventsInRange.Concat(eventsOutsideRange).ToList();
        await db.PatientEvents.AddRangeAsync(allEvents);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.GetPatientEvents(
                facilityId: facilityId,
                correlationId: null,
                startDate: startDate,
                endDate: endDate,
                sortBy: null,
                sortOrder: null,
                pageSize: 10,
                pageNumber: 1,
                cancellationToken: default
            );

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEventModel>>(okResult.Value);

            // Should only return the 4 events within the date range
            Assert.Equal(4, pagedResult.Records.Count());

            // Verify only the in-range event IDs are returned
            var returnedIds = pagedResult.Records.Select(e => e.Id).ToHashSet();
            var expectedIds = eventsInRange.Select(e => e.Id).ToHashSet();

            Assert.Equal(expectedIds, returnedIds);

            // Verify none of the outside-range events are returned
            var outsideIds = eventsOutsideRange.Select(e => e.Id).ToHashSet();
            Assert.True(returnedIds.Intersect(outsideIds).Count() == 0,
                "Events outside date range should not be returned");

            _output.WriteLine($"Returned {returnedIds.Count} events within range [{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}]");
        }
        finally
        {
            // Cleanup
            db.PatientEvents.RemoveRange(allEvents);
            await db.SaveChangesAsync();
        }
    }

    #endregion

    #region DeletePatientEvent Tests

    [Fact]
    public async Task DeletePatientEvent_WithValidId_DeletesSuccessfully()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var patientId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();

        var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow);
        var evt = payload.CreatePatientEvent(facilityId, correlationId);

        await db.PatientEvents.AddAsync(evt);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.DeletePatientEvent(evt.Id, default);

            // Assert
            Assert.IsType<AcceptedResult>(result);

            // Verify the event was deleted
            var deletedEvent = await db.PatientEvents.FindAsync(evt.Id);
            Assert.Null(deletedEvent);
        }
        finally
        {
            // Cleanup (in case test fails)
            var remainingEvent = await db.PatientEvents.FindAsync(evt.Id);
            if (remainingEvent != null)
            {
                db.PatientEvents.Remove(remainingEvent);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task DeletePatientEvent_WithEmptyId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();

        // Act
        var result = await controller.DeletePatientEvent("", default);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region DeletePatientEventsByCorrelation Tests

    [Fact]
    public async Task DeletePatientEventsByCorrelation_WithValidCorrelationId_DeletesAllMatchingEvents()
    {
        // Arrange
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();
        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();

        // Create multiple events with same correlation ID
        var events = new List<PatientEvent>();
        for (int i = 0; i < 3; i++)
        {
            var patientId = Guid.NewGuid().ToString();
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-i));
            var evt = payload.CreatePatientEvent(facilityId, correlationId);
            events.Add(evt);
        }

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        try
        {
            // Act
            var result = await controller.DeletePatientEventsByCorrelation(correlationId, default);

            // Assert
            Assert.IsType<AcceptedResult>(result);

            // Verify all events were deleted
            var remainingEvents = db.PatientEvents
                .Where(e => e.CorrelationId == correlationId)
                .ToList();
            Assert.Empty(remainingEvents);
        }
        finally
        {
            // Cleanup (in case test fails)
            var remainingEvents = db.PatientEvents
                .Where(e => e.CorrelationId == correlationId)
                .ToList();
            if (remainingEvents.Any())
            {
                db.PatientEvents.RemoveRange(remainingEvents);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task DeletePatientEventsByCorrelation_WithEmptyCorrelationId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = _fixture.ServiceProvider.GetRequiredService<PatientEventsController>();

        // Act
        var result = await controller.DeletePatientEventsByCorrelation("", default);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion
}