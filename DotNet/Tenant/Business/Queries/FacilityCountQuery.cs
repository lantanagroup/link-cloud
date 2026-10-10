using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Tenant.Business.Queries;

public static class FacilityCountQuery
{
    public static async Task<FacilityCounts> ExecuteAsync(
        IQueryable<Facility> facilities,
        IReadOnlyCollection<string>? facilityIds,
        CancellationToken cancellationToken)
    {
        var active = facilities.Where(facility => !facility.IsDeleted);
        var total = await active.CountAsync(cancellationToken);
        var test = await active.CountAsync(facility => facility.IsTest, cancellationToken);
        int? matched = null;
        if (facilityIds is not null)
        {
            matched = await active
                .Where(facility => facilityIds.Contains(facility.FacilityId))
                .CountAsync(cancellationToken);
        }

        return new FacilityCounts
        {
            Total = total,
            Matched = matched,
            Test = test
        };
    }
}
