using Automation.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunOrganizationIdAllocatorTests
{
    [Fact]
    public void Allocate_returns_a_five_digit_id()
    {
        var id = RunOrganizationIdAllocator.Allocate(new HashSet<string>());

        id.Should().MatchRegex("^\\d{5}$");
        int.Parse(id).Should().BeInRange(
            RunOrganizationIdAllocator.Minimum,
            RunOrganizationIdAllocator.ExclusiveMaximum - 1);
    }

    [Fact]
    public void Allocate_skips_an_id_an_ongoing_run_already_uses()
    {
        var draws = new Queue<int>([10_000, 10_001]);
        var inUse = new HashSet<string>(StringComparer.Ordinal) { "10000" };

        var id = RunOrganizationIdAllocator.Allocate(inUse, (_, _) => draws.Dequeue());

        id.Should().Be("10001");
    }

    [Fact]
    public void Allocate_fails_when_every_draw_is_already_in_use()
    {
        var inUse = new HashSet<string>(StringComparer.Ordinal) { "10000" };

        var act = () => RunOrganizationIdAllocator.Allocate(inUse, (_, _) => 10_000);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ongoing run*");
    }

    [Fact]
    public void CollectInUse_ignores_a_finished_run_and_keeps_an_ongoing_one()
    {
        var inUse = RunOrganizationIdAllocator.CollectInUse(
        [
            ("48291", "48291", Ongoing: false),
            ("10001", null, Ongoing: true),
            (null, "10002", Ongoing: true),
            ("  ", "10003", Ongoing: false)
        ]);

        inUse.Should().BeEquivalentTo(["10001", "10002"]);
    }

    [Fact]
    public void Two_allocations_do_not_share_an_id_once_the_first_is_reserved()
    {
        var inUse = new HashSet<string>(StringComparer.Ordinal);
        var draws = new Queue<int>([22_222, 22_222, 33_333]);

        var first = RunOrganizationIdAllocator.Allocate(inUse, (_, _) => draws.Dequeue());
        inUse.Add(first);
        var second = RunOrganizationIdAllocator.Allocate(inUse, (_, _) => draws.Dequeue());

        first.Should().Be("22222");
        second.Should().Be("33333");
    }

    [Fact]
    public void Choose_keeps_the_run_id_when_numeric_only_facility_ids_are_off()
    {
        var runId = Guid.NewGuid();
        var inUse = new HashSet<string>(StringComparer.Ordinal) { runId.ToString() };

        var id = RunOrganizationIdAllocator.Choose(numericOnlyFacilityId: false, runId, inUse, (_, _) => 10_000);

        id.Should().Be(runId.ToString());
    }

    [Fact]
    public void Choose_draws_a_five_digit_id_when_numeric_only_facility_ids_are_on()
    {
        var runId = Guid.NewGuid();
        var draws = new Queue<int>([10_000, 22_222]);
        var inUse = new HashSet<string>(StringComparer.Ordinal) { "10000" };

        var id = RunOrganizationIdAllocator.Choose(
            numericOnlyFacilityId: true,
            runId,
            inUse,
            (_, _) => draws.Dequeue());

        id.Should().Be("22222");
    }

    [Fact]
    public void Missing_facility_id_setting_binds_off()
    {
        var settings = new ConfigurationBuilder()
            .Build()
            .GetSection(FacilityIdFormatSettings.SectionName)
            .Get<FacilityIdFormatSettings>();

        (settings?.NumericOnlyFacilityId ?? false).Should().BeFalse();
    }

    [Fact]
    public void Facility_id_setting_binds_the_tenant_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [FacilityIdFormatSettings.NumericOnlyKey] = "true"
            })
            .Build();

        var settings = configuration
            .GetSection(FacilityIdFormatSettings.SectionName)
            .Get<FacilityIdFormatSettings>();

        settings!.NumericOnlyFacilityId.Should().BeTrue();
        FacilityIdFormatSettings.NumericOnlyKey.Should().Be("FacilityIdSettings:NumericOnlyFacilityId");
    }

    [Fact]
    public void Tenant_override_label_is_the_tenant_service_label()
    {
        var labels = FacilityIdFormatSettings.TenantOverrideLabels();

        labels.Should().ContainSingle();
        labels[0].Key.Should().Be("FacilityIdSettings:NumericOnlyFacilityId");
        labels[0].Value.Should().Be("Tenant");
    }
}
