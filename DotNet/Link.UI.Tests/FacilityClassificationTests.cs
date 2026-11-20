using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class FacilityClassificationTests
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_run_owns_its_run_id_even_when_the_created_flag_is_unset()
    {
        var runId = Guid.Parse("afb7185a-b94a-4580-8e89-c4e6724073aa");

        FacilityClassification.RunOwns(runId, "other-tenant", created: false, runId.ToString()).Should().BeTrue();
        FacilityClassification.RunOwns(runId, "other-tenant", created: false, " " + runId.ToString("D").ToUpperInvariant()).Should().BeTrue();
    }

    [Fact]
    public void A_created_facility_is_owned_including_a_non_guid_id()
    {
        var runId = Guid.Parse("f172a29b-71c6-4a8b-8bac-857440411867");

        FacilityClassification.RunOwns(runId, "nhsn-org", created: true, "NHSN-ORG").Should().BeTrue();
        FacilityClassification.RunOwns(runId, "10756", created: true, "10756").Should().BeTrue();
    }

    [Fact]
    public void A_reused_facility_is_not_owned_and_cannot_be_destroyed()
    {
        var runId = Guid.Parse("595c76d6-77a2-4a88-ade6-b4c4708f540a");
        var runs = new[] { new AutomationRunMark(runId, "CityHospital", false, Seen) };

        FacilityClassification.RunOwns(runId, "CityHospital", created: false, "CityHospital").Should().BeFalse();
        FacilityClassification.AllowsDestructive("CityHospital", runs, tombstoneFacilityIds: null).Should().BeFalse();
        FacilityClassification.AllowsDestructive("CityHospital", runs, ["other"]).Should().BeFalse();
    }

    [Fact]
    public void A_tombstone_allows_cleanup_and_a_blank_id_does_not()
    {
        var runId = Guid.Parse("e2d21451-2381-4df7-b15c-bb7e2a6b9c48");
        var runs = new[] { new AutomationRunMark(runId, runId.ToString(), false, Seen) };

        FacilityClassification.AllowsDestructive("gone-facility", runs, ["Gone-Facility"]).Should().BeTrue();
        FacilityClassification.AllowsDestructive(runId.ToString(), runs, tombstoneFacilityIds: null).Should().BeTrue();
        FacilityClassification.AllowsDestructive("  ", runs, ["gone-facility"]).Should().BeFalse();
        FacilityClassification.AllowsDestructive(null, runs, ["gone-facility"]).Should().BeFalse();
    }

    [Fact]
    public void Real_lists_drop_owned_rows_and_keep_the_note()
    {
        var owned = Guid.Parse("61c1f64e-b9ce-40f5-88eb-d1fa3fcfac05");
        var index = AutomationMarkRules.Build(
            [new AutomationRunMark(owned, null, false, Seen)],
            []);
        var rows = new[] { "hospital", owned.ToString(), "clinic" };

        var kept = AutomationMarkRules.DropOwned(rows, id => id, index, out var hidAny);

        hidAny.Should().BeTrue();
        kept.Should().Equal("hospital", "clinic");
        AutomationMarkRules.WithHiddenNote(null, hidAny).Should().Be(AutomationMarkRules.HiddenNote);
        AutomationMarkRules.WithHiddenNote("Already.", false).Should().Be("Already.");
    }

    [Fact]
    public void Cancel_cleanup_and_leftover_teardown_ask_the_classification()
    {
        var manager = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/AutomationRunManager.cs"));
        manager.Should().Contain("FacilityClassification.RunOwns");

        var sweeper = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/LeftoverRunCleanupService.cs"));
        sweeper.Should().Contain("FacilityClassification.AllowsDestructive");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
