using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class TenantListRulesTests
{
    [Fact]
    public void A_large_facility_list_is_paged_instead_of_rendered_whole()
    {
        var items = Enumerable.Range(1, 1000).Select(i => i.ToString()).ToList();

        var page = TenantListRules.Slice(items, 3, 25);

        page.TotalCount.Should().Be(1000);
        page.TotalPages.Should().Be(40);
        page.PageNumber.Should().Be(3);
        page.PageSize.Should().Be(25);
        page.Items.Should().HaveCount(25);
        page.Items[0].Should().Be("51");
    }

    [Fact]
    public void An_unknown_page_size_falls_back_and_a_page_past_the_end_clamps()
    {
        var items = Enumerable.Range(1, 30).Select(i => i.ToString()).ToList();

        var page = TenantListRules.Slice(items, 9, 15);

        page.PageSize.Should().Be(TenantListRules.DefaultPageSize);
        page.PageNumber.Should().Be(2);
        page.Items.Should().HaveCount(5);
        page.Items[0].Should().Be("26");
    }
}
