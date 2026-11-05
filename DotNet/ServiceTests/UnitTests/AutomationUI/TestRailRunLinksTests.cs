using System.Text;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class TestRailRunLinksTests
{
    [Theory]
    [InlineData("https://example.testrail.io")]
    [InlineData("https://example.testrail.io/")]
    [InlineData("https://example.testrail.io///")]
    [InlineData("  https://example.testrail.io/  ")]
    [InlineData("https://example.testrail.io/index.php?/api/v2")]
    [InlineData("https://example.testrail.io/index.php?/api/v2/")]
    public void Run_url_drops_a_trailing_slash_and_api_suffix(string baseUrl)
    {
        TestRailRunLinks.RunUrl(baseUrl, 292)
            .Should().Be("https://example.testrail.io/index.php?/runs/view/292");
        TestRailRunLinks.TestUrl(baseUrl, 16898)
            .Should().Be("https://example.testrail.io/index.php?/tests/view/16898");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.testrail.io")]
    [InlineData("not a url")]
    public void Run_url_is_empty_when_the_base_cannot_be_used(string? baseUrl)
    {
        TestRailRunLinks.RunUrl(baseUrl, 292).Should().BeNull();
        TestRailRunLinks.TestUrl(baseUrl, 15).Should().BeNull();
    }

    [Fact]
    public void Run_url_rejects_a_non_positive_id()
    {
        TestRailRunLinks.RunUrl("https://example.testrail.io", 0).Should().BeNull();
        TestRailRunLinks.TestUrl("https://example.testrail.io", -1).Should().BeNull();
    }

    [Fact]
    public void Capture_keeps_one_test_link_and_omits_it_when_several_tests_were_posted()
    {
        var one = TestRailRunLinks.Capture(
            "https://example.testrail.io/",
            12,
            [new TestRailResultDto { Id = 1, TestId = 99 }]);
        one!.TestId.Should().Be(99);
        one.TestUrl.Should().Be("https://example.testrail.io/index.php?/tests/view/99");

        var many = TestRailRunLinks.Capture(
            "https://example.testrail.io",
            12,
            [
                new TestRailResultDto { Id = 1, TestId = 99 },
                new TestRailResultDto { Id = 2, TestId = 100 }
            ]);
        many!.Url.Should().Be("https://example.testrail.io/index.php?/runs/view/12");
        many.TestId.Should().BeNull();
        many.TestUrl.Should().BeNull();
    }

    [Fact]
    public void AddDistinct_keeps_each_destination_run_once()
    {
        var stored = new List<TestRailRunReference>();
        var first = TestRailRunLinks.Capture("https://example.testrail.io", 10, [new TestRailResultDto { TestId = 1 }])!;
        var same = TestRailRunLinks.Capture("https://example.testrail.io/", 10, [new TestRailResultDto { TestId = 1 }])!;
        var second = TestRailRunLinks.Capture(
            "https://example.testrail.io",
            11,
            [new TestRailResultDto { Id = 2, TestId = 0 }])!;

        TestRailRunLinks.AddDistinct(stored, [first]).Should().BeTrue();
        TestRailRunLinks.AddDistinct(stored, [same, second]).Should().BeTrue();
        TestRailRunLinks.AddDistinct(stored, [second]).Should().BeFalse();

        stored.Select(run => run.RunId).Should().Equal(10, 11);
    }

    [Fact]
    public void Capture_records_nothing_when_no_results_were_posted()
    {
        TestRailRunLinks.Capture("https://example.testrail.io", 501, null).Should().BeNull();
        TestRailRunLinks.Capture("https://example.testrail.io", 501, []).Should().BeNull();
    }

    [Fact]
    public void Second_publish_of_the_same_run_id_does_not_duplicate_the_entry()
    {
        var stored = new List<TestRailRunReference>();
        var first = TestRailRunLinks.Capture(
            "https://example.testrail.io",
            501,
            [new TestRailResultDto { Id = 1, TestId = 16898 }])!;
        var again = TestRailRunLinks.Capture(
            "https://example.testrail.io/",
            501,
            [new TestRailResultDto { Id = 2, TestId = 16898 }])!;

        TestRailRunLinks.AddDistinct(stored, [first]).Should().BeTrue();
        TestRailRunLinks.AddDistinct(stored, [again]).Should().BeFalse();

        stored.Should().ContainSingle();
        stored[0].RunId.Should().Be(501);
        stored[0].TestId.Should().Be(16898);
    }

    [Fact]
    public void Export_says_not_published_until_a_run_link_exists()
    {
        var empty = new StringBuilder();
        TestRailRunLinks.AppendExport(empty, null);
        empty.ToString().Should().Contain(TestRailRunLinks.NotPublished);
        empty.ToString().Should().NotContain("index.php");

        var published = new StringBuilder();
        TestRailRunLinks.AppendExport(published,
        [
            TestRailRunLinks.Capture("https://example.testrail.io/", 292, [new TestRailResultDto { TestId = 5 }])!,
            TestRailRunLinks.Capture("https://example.testrail.io", 293, [new TestRailResultDto { Id = 3, TestId = 0 }])!
        ]);
        published.ToString().Should().NotContain(TestRailRunLinks.NotPublished);
        published.ToString().Should().Contain($"{"TestRail Run",-26}: R292");
        published.ToString().Should().Contain($"{"TestRail Run",-26}: R293");
        published.ToString().Should().Contain("https://example.testrail.io/index.php?/runs/view/292");
        published.ToString().Should().Contain("https://example.testrail.io/index.php?/runs/view/293");
        published.ToString().Should().Contain("https://example.testrail.io/index.php?/tests/view/5");
    }
}
