using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ConfigurationRulesTests
{
    [Fact]
    public void Facility_id_follows_the_numeric_rule_when_that_mode_is_on()
    {
        ConfigurationRules.CheckFacility("12a", numericOnly: true, required: true, out _).Should().NotBeNull();
        ConfigurationRules.CheckFacility("120", numericOnly: true, required: true, out var numeric).Should().BeNull();
        numeric.Should().Be("120");
        ConfigurationRules.CheckFacility("link-ui", numericOnly: false, required: false, out var text).Should().BeNull();
        text.Should().Be("link-ui");
        ConfigurationRules.CheckFacility(" ", numericOnly: false, required: true, out _).Should().NotBeNull();
    }

    [Fact]
    public void Bundle_requires_a_bundle_object_and_a_measure_id()
    {
        ConfigurationRules.CheckBundle("{\"resourceType\":\"Bundle\",\"id\":\"NHSN-1\"}", out var id).Should().BeNull();
        id.Should().Be("NHSN-1");
        ConfigurationRules.CheckBundle("{\"resourceType\":\"Patient\",\"id\":\"NHSN-1\"}", out _).Should().Contain("Bundle");
        ConfigurationRules.CheckBundle("{\"resourceType\":\"Bundle\"}", out _).Should().Contain("Bundle.id");
        ConfigurationRules.CheckBundle("{", out _).Should().Contain("JSON");
        ConfigurationRules.CheckBundle("[]", out _).Should().Contain("JSON object");
        ConfigurationRules.ReadMeasures("not-json").Should().BeNull();
        ConfigurationRules.ReadMeasures("").Should().BeEmpty();
    }

    [Fact]
    public void Code_search_rejects_a_short_search_both_filters_and_an_overlong_url()
    {
        ConfigurationRules.CheckCodeSearch(new CodeQuery { Search = "ab" }, out _).Should().Contain("at least 3");
        ConfigurationRules.CheckCodeSearch(new CodeQuery { CodeSystem = "http://loinc.org", ValueSet = "http://example.org/vs" }, out _)
            .Should().Contain("not both");
        ConfigurationRules.CheckCodeSearch(new CodeQuery { Version = "1" }, out _).Should().Contain("version");
        ConfigurationRules.CheckCodeSearch(new CodeQuery { CodeSystem = new string('u', 2001) }, out _).Should().Contain("2000");
        ConfigurationRules.CheckCodeSearch(new CodeQuery { Search = "blood", PageSize = 500 }, out var clean).Should().BeNull();
        clean.PageSize.Should().Be(100);
    }

    [Fact]
    public void Emails_are_split_deduped_and_capped()
    {
        ConfigurationRules.CheckEmails("a@example.com, A@example.com\nb@example.com", true, out var emails).Should().BeNull();
        emails.Should().Equal("a@example.com", "b@example.com");
        ConfigurationRules.CheckEmails(" ", true, out _).Should().Contain("at least one");
        ConfigurationRules.CheckEmails(" ", false, out var none).Should().BeNull();
        none.Should().BeEmpty();
        ConfigurationRules.CheckEmails("not-an-email", true, out _).Should().Contain("email");
        var many = string.Join(",", Enumerable.Range(1, 21).Select(index => $"u{index}@example.com"));
        ConfigurationRules.CheckEmails(many, true, out _).Should().Contain("20");
    }

    [Fact]
    public void Category_id_title_severity_and_guidance_are_checked()
    {
        ConfigurationRules.CheckCategory(new CategoryForm { Id = "uncategorized", Title = "t", Severity = "ERROR", Guidance = "g" }, out _)
            .Should().Contain("reserved");
        ConfigurationRules.CheckCategory(new CategoryForm { Id = "ok", Title = "Title", Severity = "nope", Guidance = "g" }, out _)
            .Should().Contain("Severity");
        ConfigurationRules.CheckCategory(new CategoryForm { Id = "ok", Title = "Title", Severity = "warning", Guidance = "" }, out _)
            .Should().Contain("Guidance");
        ConfigurationRules.CheckCategory(new CategoryForm { Id = "lab.rule", Title = "Title", Severity = "INFORMATION", Guidance = "Look here" }, out var clean)
            .Should().BeNull();
        clean.Severity.Should().Be("INFORMATION");
    }

    [Fact]
    public void Page_size_stays_on_the_offered_list()
    {
        ConfigurationRules.ListedPageSize(15, ConfigurationRules.PageSizes).Should().Be(10);
        ConfigurationRules.ListedPageSize(20, ConfigurationRules.NotificationPageSizes).Should().Be(20);
        ConfigurationRules.ListedPageSize(50, ConfigurationRules.NotificationPageSizes).Should().Be(10);
    }

    [Fact]
    public void Mapping_does_not_truncate_an_overlong_measure_and_requires_a_frequency()
    {
        ConfigurationRules.CheckMapping(new MappingForm { Measure = new string('m', 256), Dqm = "dqm", Frequency = "Daily" }, true, out _)
            .Should().Contain("255");
        ConfigurationRules.CheckMapping(new MappingForm { Measure = "m", Dqm = "d", Frequency = "Yearly" }, true, out _)
            .Should().Contain("Frequency");
        ConfigurationRules.CheckMapping(new MappingForm { Id = "not-a-guid", Measure = "m", Dqm = "d", Frequency = "adhoc" }, false, out _)
            .Should().Contain("id");
        ConfigurationRules.CheckMappingSearch(new MappingQuery { Frequency = "weekly", PageSize = 7 }, out var query).Should().BeNull();
        query.Frequency.Should().Be("Weekly");
        query.PageSize.Should().Be(10);
    }

    [Fact]
    public void Notification_dates_must_be_a_range()
    {
        ConfigurationRules.CheckNotification(new NotificationQuery { CreatedOnStart = "2026-10-08", CreatedOnEnd = "2026-10-07" }, false, out _)
            .Should().Contain("before");
        ConfigurationRules.CheckNotification(new NotificationQuery { SentOnStart = "10/07/2026" }, false, out _)
            .Should().Contain("yyyy-MM-dd");
        ConfigurationRules.CheckNotification(new NotificationQuery { FacilityId = "ok-1", PageSize = 20 }, false, out var clean).Should().BeNull();
        clean.PageSize.Should().Be(20);
    }

    [Fact]
    public void Hsloc_paging_filters_and_clamps_to_the_last_page()
    {
        var rows = Enumerable.Range(1, 25).Select(index => new HslocRow
        {
            Code = "C" + index,
            CdcCode = index == 3 ? "CDC-special" : "CDC",
            ShortDescription = "short",
            LongDescription = "long",
            Version = index < 5 ? "2024" : "2025"
        }).ToList();

        var filtered = ConfigurationRules.PageHsloc(rows, new HslocQuery { Text = "special", Version = "2024" });
        filtered.Page.Should().ContainSingle();
        filtered.Page[0].Code.Should().Be("C3");

        var last = ConfigurationRules.PageHsloc(rows, new HslocQuery { Page = 99 });
        last.Bar.Page.Should().Be(2);
        last.Page.Should().HaveCount(5);
        last.Bar.TotalCount.Should().Be(25);
    }

    [Fact]
    public void Operation_search_rejects_an_unknown_type_and_a_bad_id()
    {
        ConfigurationRules.CheckOperation(new OperationQuery { OperationType = "NotAType" }, false, out _).Should().Contain("operation type");
        ConfigurationRules.CheckOperation(new OperationQuery { OperationId = "nope" }, false, out _).Should().Contain("Operation id");
        ConfigurationRules.CheckOperation(new OperationQuery { VendorVersionId = "also-nope" }, false, out _).Should().Contain("Vendor version");
        var id = Guid.NewGuid();
        ConfigurationRules.CheckOperation(new OperationQuery { OperationType = "codemap", OperationId = id.ToString(), SortDir = "asc" }, false, out var clean)
            .Should().BeNull();
        clean.OperationType.Should().Be("CodeMap");
        clean.OperationId.Should().Be(id.ToString());
        clean.SortBy.Should().Be("CreateDate");
        clean.SortDir.Should().Be("asc");
    }
}
