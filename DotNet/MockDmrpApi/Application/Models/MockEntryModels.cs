using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.MockDmrpApi.Application.Models;

/// <summary>
/// Request and response models for the support surface.
/// </summary>
/// <remarks>
/// Hand-written rather than generated. The support surface is ours, so it has no business
/// borrowing types from the third party's contract -- if it did, replacing that contract
/// would break endpoints that have nothing to do with it.
/// <para>
/// <see cref="MockEntryRequest.ReportingMonth"/> is nullable because the rule is conditional:
/// monthly components require it, annual ones must omit it. A <c>[Range]</c> attribute cannot
/// express that, so the service enforces it and returns a message naming the component.
/// </para>
/// </remarks>
public class MockEntryRequest
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>The NHSN component, e.g. <c>MSC</c> or <c>PS</c>.</summary>
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Component { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Measure { get; set; } = string.Empty;

    /// <summary>Required for monthly components, omitted for annual ones.</summary>
    [Range(1, 12)]
    public int? ReportingMonth { get; set; }

    [Range(2000, 2100)]
    public int ReportingYear { get; set; }

    /// <summary>Defaults to <c>Y</c> when omitted.</summary>
    [StringLength(10)]
    public string? IsReporting { get; set; }
}

/// <summary>A stored entry, as returned by the support surface.</summary>
public class MockEntryModel
{
    public string Id { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string Measure { get; set; } = string.Empty;
    public int? ReportingMonth { get; set; }
    public int ReportingYear { get; set; }
    public string IsReporting { get; set; } = string.Empty;
    public DateTimeOffset CreateDate { get; set; }
    public DateTimeOffset? ModifyDate { get; set; }
}

/// <summary>An update carries the identifier as well, and it must match the route.</summary>
public class MockEntryUpdateRequest : MockEntryRequest
{
    [Required]
    public string Id { get; set; } = string.Empty;
}

public class MockEntryPage
{
    public IReadOnlyList<MockEntryModel> Records { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = new();
}

/// <summary>Translates between stored entries and the support surface's own models.</summary>
public static class MockEntryMapper
{
    public static MockEntryModel ToModel(ReportingPlanEntryEntity entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new MockEntryModel
        {
            Id = entry.Id,
            FacilityId = entry.FacilityId,
            Component = entry.Component,
            Measure = entry.Measure,
            ReportingMonth = entry.ReportingMonth,
            ReportingYear = entry.ReportingYear,
            IsReporting = entry.IsReporting,

            // The column is datetime2 with no offset, so EF hands these back Unspecified.
            // Left unqualified the conversion would apply the host's local offset, and a
            // developer would see different timestamps than CI.
            CreateDate = new DateTimeOffset(DateTime.SpecifyKind(entry.CreateDate, DateTimeKind.Utc)),
            ModifyDate = entry.ModifyDate is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(entry.ModifyDate.Value, DateTimeKind.Utc))
        };
    }

    /// <summary>
    /// Builds an entity from a create request. The identifier and timestamps are left unset:
    /// the service assigns the first, the save interceptor sets the others.
    /// </summary>
    public static ReportingPlanEntryEntity ToEntity(MockEntryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ReportingPlanEntryEntity
        {
            FacilityId = request.FacilityId,
            Component = request.Component,
            Measure = request.Measure,
            ReportingMonth = request.ReportingMonth,
            ReportingYear = request.ReportingYear,
            IsReporting = string.IsNullOrWhiteSpace(request.IsReporting) ? "Y" : request.IsReporting
        };
    }

    public static ReportingPlanEntryEntity ToEntity(MockEntryUpdateRequest request)
    {
        var entity = ToEntity((MockEntryRequest)request);
        entity.Id = request.Id;
        return entity;
    }

    public static MockEntryPage ToPage(
        IReadOnlyList<ReportingPlanEntryEntity> records, PaginationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(metadata);

        return new MockEntryPage
        {
            Records = records.Select(ToModel).ToList(),
            Metadata = metadata
        };
    }
}
