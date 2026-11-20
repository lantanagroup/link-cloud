using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Business;

/// <summary>
/// Converts the facility API model into the Tenant entity that is saved and scheduled.
/// </summary>
public static class FacilityModelMapping
{
    /// <summary>
    /// Returns a new <see cref="Facility"/> holding the values of <paramref name="model"/>.
    /// </summary>
    public static Facility ToEntity(FacilityModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Missing values get the defaults the earlier AutoMapper mapping gave them, which
        // FacilityModelMappingTests pins: the null-forgiving operators keep those nulls as they were.
        return new Facility
        {
            Id = model.Id ?? Guid.Empty,
            FacilityId = model.FacilityId!,
            FacilityName = model.FacilityName,
            TimeZone = model.TimeZone,
            IsDeleted = model.IsDeleted ?? false,
            VendorVersionId = model.VendorVersionId,
            ScheduledReports = ToScheduledReportModel(model.ScheduledReports)!
        };
    }

    private static ScheduledReportModel? ToScheduledReportModel(TenantScheduledReportConfig? config)
    {
        if (config is null)
        {
            return null;
        }

        return new ScheduledReportModel
        {
            Daily = CopyOrEmpty(config.Daily),
            Weekly = CopyOrEmpty(config.Weekly),
            Monthly = CopyOrEmpty(config.Monthly)
        };
    }

    private static string[] CopyOrEmpty(string[]? values)
    {
        if (values is null)
        {
            return [];
        }

        return [.. values];
    }
}
