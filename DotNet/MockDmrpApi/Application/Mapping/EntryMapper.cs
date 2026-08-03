using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.MockDmrpApi.Application.Mapping;

/// <summary>
/// Translates between the persisted entity and the generated contract types.
/// </summary>
/// <remarks>
/// This is the seam that keeps the API contract out of the database. Everything below it
/// deals in <see cref="ReportingPlanEntryEntity"/>; everything above it deals in generated types.
/// When Contracts/dmrp-openapi.yaml is replaced, the compile errors land here rather than
/// in the service layer or a migration.
/// </remarks>
public static class EntryMapper
{
    public static ReportingPlanEntry ToContract(ReportingPlanEntryEntity entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ReportingPlanEntry
        {
            Id = entry.Id,
            FacilityId = entry.FacilityId,
            Measure = entry.Measure,
            ReportingMonth = entry.ReportingMonth,
            ReportingYear = entry.ReportingYear,
            IsReporting = entry.IsReporting,
            CreateDate = new DateTimeOffset(DateTime.SpecifyKind(entry.CreateDate, DateTimeKind.Utc)),
            ModifyDate = entry.ModifyDate is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(entry.ModifyDate.Value, DateTimeKind.Utc))
        };
    }

    /// <summary>
    /// Builds an entity from a create request. Id and the timestamps are left unset --
    /// the service assigns the identifier and the save interceptor sets the timestamps.
    /// </summary>
    public static ReportingPlanEntryEntity ToEntity(ReportingPlanEntryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ReportingPlanEntryEntity
        {
            FacilityId = request.FacilityId,
            Measure = request.Measure,
            ReportingMonth = request.ReportingMonth,
            ReportingYear = request.ReportingYear,
            IsReporting = string.IsNullOrWhiteSpace(request.IsReporting) ? "Y" : request.IsReporting
        };
    }

    /// <summary>Builds an entity from an update request, carrying the supplied identifier.</summary>
    public static ReportingPlanEntryEntity ToEntity(ReportingPlanEntry request)
    {
        var entity = ToEntity((ReportingPlanEntryRequest)request);
        entity.Id = request.Id;
        return entity;
    }

    public static ReportingPlanEntryPage ToPage(
        IReadOnlyList<ReportingPlanEntryEntity> records, PaginationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(metadata);

        return new ReportingPlanEntryPage
        {
            Records = records.Select(ToContract).ToList(),
            Metadata = new PageMetadata
            {
                PageSize = metadata.PageSize,
                PageNumber = metadata.PageNumber,
                TotalCount = metadata.TotalCount,
                TotalPages = (int)metadata.TotalPages
            }
        };
    }

    /// <summary>
    /// Projects a facility's entries for one period into a reporting plan.
    /// </summary>
    /// <remarks>
    /// Only the supplied entries appear in <c>measures</c>. A measure the facility is not
    /// enrolled in is simply absent -- there is no negative representation -- so an empty
    /// collection produces an empty measures array rather than an error or a null.
    /// </remarks>
    public static ReportingPlanResponse ToReportingPlan(
        string facilityId,
        int reportingMonth,
        int reportingYear,
        IReadOnlyList<ReportingPlanEntryEntity> entries,
        DateTimeOffset retrievedOn)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new ReportingPlanResponse
        {
            FacilityId = facilityId,
            ReportingMonth = reportingMonth,
            ReportingYear = reportingYear,
            Measures = entries
                .Select(e => new ReportingPlanMeasure
                {
                    Measure = e.Measure,
                    IsReporting = e.IsReporting
                })
                .ToList(),
            RetrievedOn = retrievedOn
        };
    }
}
