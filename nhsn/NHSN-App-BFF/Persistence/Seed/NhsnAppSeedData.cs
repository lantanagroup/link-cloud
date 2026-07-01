using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Seed;

public static class NhsnAppSeedData
{
    public static async Task SeedAsync(NhsnAppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var roleDefinitions = new[]
        {
            new { Name = NhsnAppConstants.Roles.SystemAdmin, Description = "Full administrative access for NHSN App integration testing." },
            new { Name = NhsnAppConstants.Roles.FacilityAdmin, Description = "Administrative access for facility onboarding and maintenance." },
            new { Name = NhsnAppConstants.Roles.FacilityIt, Description = "Technical configuration access for facility system integration." }
        };

        foreach (var definition in roleDefinitions)
        {
            var existing = await dbContext.Roles.SingleOrDefaultAsync(x => x.Name == definition.Name, cancellationToken);
            if (existing is null)
            {
                dbContext.Roles.Add(new NhsnRole
                {
                    Name = definition.Name,
                    Description = definition.Description
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}