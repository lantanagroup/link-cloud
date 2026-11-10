using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class PrequalPageRulesTests
{
    private const string Counts = """
        {
          "inProgress": true,
          "categories": [
            {"categoryId":"cat-1","title":"Timing","acceptable":false,"issues":3,"severity":"error","guidance":"Fix clocks"},
            {"categoryId":"Uncategorized","title":"Uncategorized","acceptable":false,"issues":1,"guidance":"wrong"},
            {"categoryId":"ok","title":"Ok","acceptable":true,"issues":0,"guidance":"skip"},
            {"categoryId":"","title":"blank","issues":4},
            {"categoryId":"fine","title":"Fine","acceptable":true,"issues":2,"guidance":"Allowed"}
          ]
        }
        """;

    [Fact]
    public void Stored_categories_drop_empty_rows_and_keep_uncategorized_guidance()
    {
        var rows = ReportsRules.ReadStoredCategories(Counts, out var inProgress);

        inProgress.Should().BeTrue();
        rows.Select(row => row.Id).Should().Equal("cat-1", "uncategorized", "fine");
        rows[1].Guidance.Should().Be(ReportsRules.UncategorizedGuidance);
        rows[1].Issues.Should().Be(1);
        rows[2].Acceptable.Should().BeTrue();
    }

    [Fact]
    public void A_missing_or_false_progress_flag_is_not_in_progress()
    {
        ReportsRules.ReadStoredCategories("""{"categories":[]}""", out var missing).Should().BeEmpty();
        missing.Should().BeFalse();
        ReportsRules.ReadStoredCategories("""{"inProgress":false,"categories":[{"categoryId":"a","issues":1}]}""", out var off);
        off.Should().BeFalse();
        ReportsRules.ReadStoredCategories("not-json", out var broken).Should().BeEmpty();
        broken.Should().BeFalse();
    }

    [Fact]
    public void Category_sort_uses_the_three_columns_and_leaves_other_sorts_in_api_order()
    {
        var rows = ReportsRules.ReadStoredCategories(Counts, out _);

        ReportsRules.SortCategories(rows, "quantity", false).Select(row => row.Issues).Should().Equal(1, 2, 3);
        ReportsRules.SortCategories(rows, "name", false).Select(row => row.Title).Should().Equal("Fine", "Timing", "Uncategorized");
        ReportsRules.SortCategories(rows, "guidance", true).First().Guidance.Should().Be(ReportsRules.UncategorizedGuidance);
        ReportsRules.SortCategories(rows, "severity", false).Select(row => row.Id).Should().Equal("cat-1", "uncategorized", "fine");
    }

    [Fact]
    public void An_issue_page_keeps_the_server_total_when_the_payload_is_one_page()
    {
        const string json = """
            {
              "records":[{"message":"m","patientId":"p","expression":"e","location":"l","code":"c","profile":"http://example/Profile|1"}],
              "metadata":{"pageNumber":2,"pageSize":10,"totalCount":40,"totalPages":4}
            }
            """;

        var page = ReportsRules.ReadCategoryIssuePage(json);

        page.Records.Should().ContainSingle();
        page.Records[0].Message.Should().Be("m");
        page.Records[0].PatientId.Should().Be("p");
        page.Paging.Page.Should().Be(2);
        page.Paging.PageSize.Should().Be(10);
        page.Paging.TotalCount.Should().Be(40);
        page.Paging.TotalPages.Should().Be(4);
        page.Records.Count.Should().BeLessThan((int)page.Paging.TotalCount);
    }

    [Fact]
    public void A_blank_issue_page_is_empty()
    {
        var page = ReportsRules.ReadCategoryIssuePage(" ");
        page.Records.Should().BeEmpty();
        page.Paging.Page.Should().Be(1);
    }

    [Fact]
    public void Prequal_query_keeps_category_sorts_only()
    {
        var rejected = ReportsRules.NormalizePrequalQuery("cat-1", "severity", "desc", 0, 7);
        rejected.Sort.Should().BeEmpty();
        rejected.Descending.Should().BeFalse();
        rejected.Page.Should().Be(1);
        rejected.PageSize.Should().Be(ReportsRules.DefaultIssuePageSize);
        rejected.Category.Should().Be("cat-1");

        var kept = ReportsRules.NormalizePrequalQuery("uncategorized", "quantity", "desc", 3, 50);
        kept.Sort.Should().Be("quantity");
        kept.Descending.Should().BeTrue();
        kept.Page.Should().Be(3);
        kept.PageSize.Should().Be(50);
    }

    [Fact]
    public void Prequal_links_carry_the_category_and_omit_direction_until_a_sort_is_chosen()
    {
        var plain = ReportsRules.PrequalHref("fac", "rep", "/tenants", "uncategorized", "", false, 2, 25);
        plain.Should().StartWith("/Reports/Validation?");
        plain.Should().Contain("category=uncategorized");
        plain.Should().Contain("page=2");
        plain.Should().NotContain("dir=");

        var sorted = ReportsRules.PrequalHref("fac", "rep", null, null, "name", true, 1, 10);
        sorted.Should().Contain("sort=name");
        sorted.Should().Contain("dir=desc");
        sorted.Should().NotContain("category=");
    }

    [Theory]
    [InlineData("uncategorized", false)]
    [InlineData("Uncategorized", false)]
    [InlineData("timing", true)]
    [InlineData(" ", false)]
    public void Uncategorized_has_no_drilldown(string categoryId, bool drilldown) =>
        ReportsRules.CategoryDrilldown(categoryId).Should().Be(drilldown);
}
