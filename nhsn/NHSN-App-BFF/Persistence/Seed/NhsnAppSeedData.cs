using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Seed;

// Seeds BFF-owned reference data — data that is the same for every facility and ships with the
// application rather than being captured from a user.
//
// Also holds the eventual job of per-vendor query-plan templates; until that table exists there is
// nothing more to seed for it here. It must never seed facilities or users — both are created on
// demand from a validated JWT, and seeding either would fabricate facility context that no token
// vouches for.
public static class NhsnAppSeedData
{
    public static async Task SeedAsync(NhsnAppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await SeedMrnIntakeOptionsAsync(dbContext, cancellationToken);

        // Future: seed VendorQueryPlanTemplates here — Epic and Cerner templates, versioned, with
        // IsActive. Reference data only.
    }

    // Values/order/labelKey mirror the former NHSN-App-UI mrn-intake/options.ts exactly, so moving
    // them here changes nothing the user sees.
    private static async Task SeedMrnIntakeOptionsAsync(NhsnAppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.MrnIntakeOptionSets.AnyAsync(cancellationToken))
        {
            return;
        }

        var rows = new List<MrnIntakeOptionSet>();
        AddGroup(rows, "MultipleMrnType", "onboarding:mrnIntake.multipleMrn.types.", [
            "empi", "legacy", "facility", "visit", "other"
        ]);
        AddGroup(rows, "MrnVarianceType", "onboarding:mrnIntake.variance.types.", [
            ("facility", "facility"), ("campus", "campus"), ("setting", "setting"), ("ehr_instance", "ehrInstance"), ("other", "other")
        ]);
        AddGroup(rows, "MrnChangeType", "onboarding:mrnIntake.changes.types.", [
            "merge", "transfer", "correction", "other"
        ]);

        dbContext.MrnIntakeOptionSets.AddRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Value and labelKey suffix are identical (e.g. "empi" -> "...types.empi").
    private static void AddGroup(List<MrnIntakeOptionSet> rows, string optionGroup, string labelKeyPrefix, IReadOnlyList<string> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            rows.Add(new MrnIntakeOptionSet
            {
                OptionGroup = optionGroup,
                Value = values[i],
                LabelKey = labelKeyPrefix + values[i],
                SortOrder = i
            });
        }
    }

    // Value and labelKey suffix differ (e.g. "ehr_instance" -> "...types.ehrInstance").
    private static void AddGroup(List<MrnIntakeOptionSet> rows, string optionGroup, string labelKeyPrefix, IReadOnlyList<(string Value, string LabelKeySuffix)> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            rows.Add(new MrnIntakeOptionSet
            {
                OptionGroup = optionGroup,
                Value = values[i].Value,
                LabelKey = labelKeyPrefix + values[i].LabelKeySuffix,
                SortOrder = i
            });
        }
    }
}
