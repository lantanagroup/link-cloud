using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class TenantListSearchTests
{
    [Fact]
    public void Exact_id_is_looked_up_only_when_the_name_list_missed_it()
    {
        TenantListSearch.ShouldLookupExactId(null, ["a"]).Should().BeFalse();
        TenantListSearch.ShouldLookupExactId("  ", ["a"]).Should().BeFalse();
        TenantListSearch.ShouldLookupExactId("zz overnight", ["a"]).Should().BeFalse();
        TenantListSearch.ShouldLookupExactId("a/b", ["a"]).Should().BeFalse();
        TenantListSearch.ShouldLookupExactId("zz-overnight-ten-032723", ["zz-overnight-ten-032723"]).Should().BeFalse();
        TenantListSearch.ShouldLookupExactId("ZZ-overnight-ten-032723", ["zz-overnight-ten-032723"]).Should().BeFalse();
        TenantListSearch.ShouldLookupExactId("zz-overnight-ten-032723", ["other"]).Should().BeTrue();
    }

    [Fact]
    public void Exact_id_joins_the_list_and_stays_deleted_when_it_was_removed()
    {
        var active = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kept"] = "Kept"
        };
        var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kept"] = "Kept"
        };

        TenantListSearch.AddExact(active, all, includeDeleted: true, "zz-overnight-ten-032723", "zz name", isDeleted: true);

        active.Should().NotContainKey("zz-overnight-ten-032723");
        all["zz-overnight-ten-032723"].Should().Be("zz name");

        TenantListSearch.AddExact(active, all, includeDeleted: false, "hidden", "Hidden", isDeleted: true);
        all.Should().NotContainKey("hidden");

        TenantListSearch.AddExact(active, all, includeDeleted: false, "live-id", "Live name", isDeleted: false);
        active["live-id"].Should().Be("Live name");
        all["live-id"].Should().Be("Live name");
    }

    [Fact]
    public void A_failed_deleted_list_does_not_mark_a_removed_facility_active()
    {
        var active = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        TenantListSearch.AddExact(active, active, includeDeleted: true, "gone", "Gone", isDeleted: true);
        active.Should().BeEmpty();
    }
}
