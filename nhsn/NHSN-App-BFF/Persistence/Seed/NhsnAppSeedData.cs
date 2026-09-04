namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Seed;

// Seeds BFF-owned reference data — data that is the same for every facility and ships with the
// application rather than being captured from a user.
//
// Its eventual job is per-vendor query-plan templates; until that table exists it runs and does
// nothing, kept rather than deleted so the seeding entry point exists exactly once. It must never
// seed facilities or users — both are created on demand from a validated JWT, and seeding either
// would fabricate facility context that no token vouches for.
public static class NhsnAppSeedData
{
    public static async Task SeedAsync(NhsnAppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        // Future: seed VendorQueryPlanTemplates here — Epic and Cerner templates, versioned, with
        // IsActive. Reference data only.
        await Task.CompletedTask;
    }
}
