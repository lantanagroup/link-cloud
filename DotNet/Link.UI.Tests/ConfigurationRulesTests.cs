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
    public void Package_name_bulk_import_and_matchers_follow_the_validation_contract()
    {
        ConfigurationRules.CheckPackageName("nhsn.ig", out var name).Should().BeNull();
        name.Should().Be("nhsn.ig");
        ConfigurationRules.CheckPackageName("bad name", out _).Should().Contain("Package name");
        ConfigurationRules.CheckBulkImport("[]").Should().Contain("No categories");
        ConfigurationRules.CheckBulkImport("{").Should().Contain("JSON");
        ConfigurationRules.CheckBulkImport("[{\"id\":\"uncategorized\",\"title\":\"t\",\"severity\":\"ERROR\",\"guidance\":\"g\",\"matcher\":{\"field\":\"CODE\",\"regex\":\"a\",\"inverted\":false}}]")
            .Should().Contain("reserved");

        var duplicate = "[" + Snapshot("same") + "," + Snapshot("same") + "]";
        ConfigurationRules.CheckBulkImport(duplicate).Should().Contain("Duplicate");
        ConfigurationRules.CheckBulkImport("[" + Snapshot("lab.rule") + "]").Should().BeNull();
        ConfigurationRules.CheckBulkImport("[{\"id\":\"lab.rule\",\"title\":\"t\",\"severity\":\"ERROR\",\"guidance\":\"g\",\"matcher\":{\"children\":[]}}]")
            .Should().Contain("at least one child");

        ConfigurationRules.CheckRule("code", "^abc", false, null, out var built).Should().BeNull();
        built.Should().Contain("\"field\":\"CODE\"");
        built.Should().Contain("\"regex\":\"^abc\"");
        ConfigurationRules.CheckRule("CODE", "[", false, null, out _).Should().Contain("valid pattern");
        ConfigurationRules.CheckRule(null, null, false, "{\"field\":\"nope\",\"regex\":\"a\"}", out _).Should().Contain("Field");
        ConfigurationRules.CheckRule(null, null, false, "{\"children\":[{\"field\":\"MESSAGE\",\"regex\":\"x\",\"inverted\":false}],\"requiresAllChildren\":true,\"inverted\":false}", out var composite)
            .Should().BeNull();
        composite.Should().Contain("children");
        ConfigurationRules.CheckRule(null, null, false, "{\"field\":\"CODE\",\"regex\":\"a\",\"children\":[{\"field\":\"CODE\",\"regex\":\"b\"}]}", out _)
            .Should().Contain("not both");
    }

    [Fact]
    public void Cql_range_parameters_and_notification_send_are_checked_before_a_call()
    {
        ConfigurationRules.CheckCql("Lib_1", "37:1-38:22", out var library, out var range).Should().BeNull();
        library.Should().Be("Lib_1");
        range.Should().Be("37:1-38:22");
        ConfigurationRules.CheckCql("Lib_1", "37-38", out _, out _).Should().Contain("37:1-38:22");
        ConfigurationRules.CheckCql(" ", null, out _, out _).Should().Contain("Library");

        ConfigurationRules.CheckEvaluate("{\"resourceType\":\"Parameters\"}", "groups,expressions", out var debug).Should().BeNull();
        debug.Should().Be("groups,expressions");
        ConfigurationRules.CheckEvaluate("{\"resourceType\":\"Bundle\"}", null, out _).Should().Contain("Parameters");
        ConfigurationRules.CheckEvaluate("{", null, out _).Should().Contain("JSON");
        ConfigurationRules.CheckEvaluate("{\"resourceType\":\"Parameters\"}", "groups,nope", out _).Should().Contain("Debug");
        ConfigurationRules.CheckEvaluate("{\"resourceType\":\"Parameters\"}", "ALL", out var all).Should().BeNull();
        all.Should().Be("all");

        ConfigurationRules.CheckSend(new NotificationSendForm
        {
            NotificationType = "Test Notification",
            Subject = "Hello",
            Body = "Body",
            Recipients = "a@example.com"
        }, numericOnly: false, out var sent, out var recipients, out _).Should().BeNull();
        sent.NotificationType.Should().Be("Test Notification");
        recipients.Should().Equal("a@example.com");
        ConfigurationRules.CheckSend(new NotificationSendForm
        {
            NotificationType = "Test Notification",
            Subject = "Hello",
            Body = "Body"
        }, numericOnly: false, out _, out _, out _).Should().Contain("recipient");
        ConfigurationRules.CheckSend(new NotificationSendForm
        {
            NotificationType = "Other",
            Subject = "Hello",
            Body = "Body",
            Recipients = "a@example.com"
        }, numericOnly: false, out _, out _, out _).Should().Contain("Type");
    }

    [Fact]
    public void Library_ids_dependencies_and_rules_are_read_from_service_json()
    {
        const string measure = """
            {"bundle":{"entry":[
              {"resource":{"resourceType":"Library","url":"http://example.org/Library/NHSN"}},
              {"resource":{"resourceType":"Measure","url":"http://example.org/Measure/NHSN"}}
            ]}}
            """;
        using var document = System.Text.Json.JsonDocument.Parse(measure);
        ConfigurationRules.LibraryIds(document.RootElement).Should().Equal("NHSN");

        var dependencies = ConfigurationRules.ReadDependencies(
            "[{\"url\":\"http://example.org/vs\",\"version\":\"1\",\"resourceExists\":true,\"versionExists\":false,\"sourceProfile\":[{\"url\":\"http://example.org/p\"}]}]",
            out var shortened);
        shortened.Should().BeFalse();
        dependencies.Should().ContainSingle();
        dependencies![0].ResourceExists.Should().BeTrue();
        dependencies[0].VersionExists.Should().BeFalse();
        dependencies[0].SourceCount.Should().Be(1);
        ConfigurationRules.ReadDependencies("{", out _).Should().BeNull();

        var rules = ConfigurationRules.ReadRules("[{\"id\":12,\"timestamp\":\"2024-01-02T03:04:05Z\",\"matcher\":{\"field\":\"CODE\",\"regex\":\"^a\",\"inverted\":true}}]");
        rules.Should().ContainSingle();
        rules![0].Id.Should().Be(12);
        rules[0].Inverted.Should().BeTrue();
        rules[0].Summary.Should().Contain("CODE");
        ConfigurationRules.ReadCreatedId("{\"id\":\"8d8c6e5a-1b2c-4d3e-9f70-1234567890ab\"}").Should().NotBeNull();
        ConfigurationRules.ReadCreatedId("{\"id\":\"not an id\"}").Should().BeNull();
    }

    private static string Snapshot(string id) =>
        "{\"id\":\"" + id + "\",\"title\":\"Title\",\"severity\":\"ERROR\",\"guidance\":\"Look\",\"matcher\":{\"field\":\"CODE\",\"regex\":\"^a\",\"inverted\":false}}";

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

    [Fact]
    public void Operation_search_keeps_a_vendor_editor_route_and_ignores_a_bad_one()
    {
        var id = Guid.NewGuid();
        ConfigurationRules.CheckOperation(
                new OperationQuery { EditType = "copyproperty", EditId = id.ToString() },
                false,
                out var clean)
            .Should().BeNull();
        clean.EditType.Should().Be("CopyProperty");
        clean.EditId.Should().Be(id.ToString());

        ConfigurationRules.CheckOperation(
                new OperationQuery { EditType = "not-a-type", EditId = "nope" },
                false,
                out var ignored)
            .Should().BeNull();
        ignored.EditType.Should().BeNull();
        ignored.EditId.Should().BeNull();
    }
}
