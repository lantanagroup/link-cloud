using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP;

/// <summary>
/// An in-memory Mock DMRP support surface, so a test can state the entries before a save and assert the
/// entries after it.
/// </summary>
internal sealed class FakeMockDmrpServiceClient : IMockDmrpServiceClient
{
    public List<MockDmrpEntryResponse> Entries { get; } = [];

    /// <summary>
    /// When set, every call answers with this status and changes nothing. 0 stands for "no response".
    /// </summary>
    public int? FailWith { get; set; }

    public int Writes { get; private set; }

    public bool DeletedByFacility { get; private set; }

    public MockDmrpEntryResponse Seed(string facilityId, string component, string measure, int month, int year,
                                      string isReporting = "Y")
    {
        var entry = new MockDmrpEntryResponse
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Component = component,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting
        };

        Entries.Add(entry);

        return entry;
    }

    public IEnumerable<string> Measures(string facilityId, int month, int year) =>
        Entries
            .Where(e => e.FacilityId == facilityId && e.ReportingMonth == month && e.ReportingYear == year)
            .Select(e => $"{e.Component}/{e.Measure}/{e.IsReporting}")
            .Order();

    public Task<LinkApiResponse<MockDmrpEntryPage>> SearchEntriesAsync(string? facilityId = null,
        string? component = null, string? measure = null, int? reportingMonth = null, int? reportingYear = null,
        int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        if (FailWith is { } status)
        {
            return Task.FromResult(new LinkApiResponse<MockDmrpEntryPage> { StatusCode = status });
        }

        var matches = Entries
            .Where(e => facilityId is null || e.FacilityId == facilityId)
            .Where(e => component is null || e.Component == component)
            .Where(e => measure is null || e.Measure == measure)
            .Where(e => reportingMonth is null || e.ReportingMonth == reportingMonth)
            .Where(e => reportingYear is null || e.ReportingYear == reportingYear)
            .ToList();

        var page = new MockDmrpEntryPage
        {
            Records = matches.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
            Metadata = new PaginationMetadata(pageSize, pageNumber, matches.Count)
        };

        return Task.FromResult(new LinkApiResponse<MockDmrpEntryPage> { StatusCode = 200, Body = page });
    }

    public Task<LinkApiResponse<MockDmrpEntryResponse>> CreateEntryAsync(MockDmrpEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (FailWith is { } status)
        {
            return Task.FromResult(new LinkApiResponse<MockDmrpEntryResponse> { StatusCode = status });
        }

        Writes++;
        var created = Seed(request.FacilityId, request.Component, request.Measure, request.ReportingMonth,
            request.ReportingYear, request.IsReporting);

        return Task.FromResult(new LinkApiResponse<MockDmrpEntryResponse> { StatusCode = 201, Body = created });
    }

    public Task<LinkApiResponse> DeleteEntryAsync(string id, CancellationToken cancellationToken = default)
    {
        if (FailWith is { } status)
        {
            return Task.FromResult(new LinkApiResponse { StatusCode = status });
        }

        Writes++;
        var removed = Entries.RemoveAll(e => e.Id == id);

        return Task.FromResult(new LinkApiResponse { StatusCode = removed > 0 ? 204 : 404 });
    }

    public Task<LinkApiResponse> DeleteEntriesForFacilityAsync(string facilityId,
        CancellationToken cancellationToken = default)
    {
        if (FailWith is { } status)
        {
            return Task.FromResult(new LinkApiResponse { StatusCode = status });
        }

        DeletedByFacility = true;
        Entries.RemoveAll(e => e.FacilityId == facilityId);

        return Task.FromResult(new LinkApiResponse { StatusCode = 204 });
    }
}
