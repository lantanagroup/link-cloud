using DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Queries;

/// <summary>
/// GET FhirQueries omits correlationId, patientId, and resourceType.
/// Those null filters must read the facility's stored queries rather than throw.
/// </summary>
[Trait("Category", "UnitTests")]
public class FhirQueryQueriesNullParameterTests
{
    [Fact]
    public async Task Unknown_facility_with_null_parameters_returns_no_queries()
    {
        await using var db = CreateInMemoryContext();
        var queries = new FhirQueryQueries(db);

        var result = await queries.GetFhirQueriesAsync("facility-unknown", null, null, null);

        Assert.NotNull(result.Queries);
        Assert.Empty(result.Queries);
    }

    [Fact]
    public async Task Existing_queries_are_returned_when_optional_parameters_are_null()
    {
        await using var db = CreateInMemoryContext();
        db.FhirQueries.Add(new FhirQuery
        {
            Id = Guid.NewGuid(),
            FacilityId = "facility-1",
            QueryType = FhirQueryType.Search,
            QueryParameters = new List<string> { "patient=patient-1" },
            DataAcquisitionLogId = 1
        });
        await db.SaveChangesAsync();

        var queries = new FhirQueryQueries(db);
        var result = await queries.GetFhirQueriesAsync("facility-1", null, null, null);

        var stored = Assert.Single(result.Queries);
        Assert.Equal("facility-1", stored.FacilityId);
    }

    [Fact]
    public async Task Correlation_filter_excludes_other_logs()
    {
        await using var db = CreateInMemoryContext();
        db.DataAcquisitionLogs.AddRange(
            Log(1, "facility-1", "correlation-1", "patient-1"),
            Log(2, "facility-1", "correlation-2", "patient-2"));
        db.FhirQueries.AddRange(
            Query("facility-1", 1),
            Query("facility-1", 2));
        await db.SaveChangesAsync();

        var queries = new FhirQueryQueries(db);
        var result = await queries.GetFhirQueriesAsync("facility-1", "correlation-1", null, null);

        var stored = Assert.Single(result.Queries);
        Assert.Equal(1, stored.DataAcquisitionLogId);
    }

    private static DataAcquisitionLog Log(long id, string facilityId, string correlationId, string patientId) => new()
    {
        Id = id,
        FacilityId = facilityId,
        CorrelationId = correlationId,
        PatientId = patientId
    };

    private static FhirQuery Query(string facilityId, long logId) => new()
    {
        Id = Guid.NewGuid(),
        FacilityId = facilityId,
        QueryType = FhirQueryType.Read,
        QueryParameters = new List<string>(),
        DataAcquisitionLogId = logId
    };

    private static DataAcquisitionDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<DataAcquisitionDbContext>()
            .UseInMemoryDatabase($"FhirQueryQueries_{Guid.NewGuid():N}")
            .Options;
        return new DataAcquisitionDbContext(options);
    }
}
