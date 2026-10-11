using System.IO;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Xunit;

namespace Link.UI.Tests;

public class LastIssuesFixesTests
{
    private static string Dot(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "DotNet" }.Concat(parts).ToArray()));
    }

    private static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, selector);
        return css.Substring(start, css.IndexOf('}', start) - start);
    }

    [Fact]
    public void Dashboard_bars_use_the_status_palette_not_black()
    {
        var bar = Rule(Dot("Link.UI", "wwwroot", "css", "site.css"), ".home-trend-bar");
        Assert.Contains("var(--au-success)", bar);
        Assert.DoesNotContain("accent-fill", bar);
    }

    [Fact]
    public void Copy_icon_floats_outside_the_text_without_layout_space()
    {
        var css = Dot("Link.UI", "wwwroot", "css", "site.css");
        Assert.Contains("transform: translateY(-55%);", css); Assert.DoesNotContain("td.lu-copy-host {\n    padding-right", css.Replace("\r\n", "\n"));
        Assert.Contains("@media (hover: hover) and (pointer: fine) {", css);
    }

    [Fact]
    public void Broker_id_cell_is_a_plain_link()
    {
        var view = Dot("Link.UI", "Views", "Operations", "_Brokers.cshtml");
        Assert.DoesNotContain("Label = \"Broker\", Value = row.Id", view);
        Assert.Contains("title=\"Open broker @row.Id\">@row.Id</a></td>", view);
    }

    [Fact]
    public void Produce_form_is_disabled_up_front_for_a_topic_outside_the_catalog()
    {
        var view = Dot("Link.UI", "Views", "Operations", "_ProduceMessage.cshtml");
        Assert.Contains("KafkaBrowseAllowList.Admit(topicName, null).Allowed", view);
        Assert.Contains("<fieldset id=\"produce-fields\" disabled=\"@refused\">", view); Assert.Contains("id=\"kafka-produce-refused\"", view);
        Assert.False(KafkaBrowseAllowList.Admit("loadtest-scratch", null).Allowed);
        Assert.True(KafkaBrowseAllowList.Admit("ResourcesAcquired", null).Allowed);
    }

    [Fact]
    public void Normalization_default_suite_retries_write_conflicts()
    {
        var source = Dot("Link.UI", "Engine", "Services", "Persistence", "MongoNormalizationStore.cs");
        var body = source.Substring(source.IndexOf("SetDefaultSuiteAsync(Guid id", StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("DeleteSuiteAsync", StringComparison.Ordinal));
        Assert.Contains("WithTransactionAsync", body);
        Assert.DoesNotContain("StartTransaction()", body);
    }

    [Fact]
    public void Migration_reconcile_logs_a_repeated_failure_once()
    {
        var source = Dot("Admin.BFF", "Application", "KafkaOps", "Migration", "MigrationRuntime.cs");
        Assert.Contains("Interlocked.Exchange(ref _reconcileFailure, reason) != reason", source);
        Assert.Contains("_logger.LogTrace(\"Migration reconcile still not finished", source);
    }

    [Fact]
    public void No_table_column_is_hidden_at_laptop_width()
    {
        var css = Dot("Link.UI", "wwwroot", "css", "site.css");
        foreach (System.Text.RegularExpressions.Match rule in System.Text.RegularExpressions.Regex.Matches(css, @"([^{}]*)\{([^{}]*)\}"))
        {
            var selector = rule.Groups[1].Value;
            var body = rule.Groups[2].Value;
            if (body.Contains("display: none") || body.Contains("display:none"))
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(selector, @"\b(th|td)(:nth-child|:nth-of-type|\.)"), "Column-hiding rule: " + selector.Trim());
        }
        Assert.Contains("<td class=\"lu-created\">@report.Created</td>", Dot("Link.UI", "Views", "Reports", "Index.cshtml"));
        Assert.Contains("<td class=\"lu-created\">@report.Created</td>", Dot("Link.UI", "Views", "Tenants", "_ViewReports.cshtml"));
    }

    [Fact]
    public void Wide_tables_move_secondary_columns_to_their_detail_views()
    {
        var groups = Dot("Link.UI", "Views", "Operations", "_Consumers.cshtml");
        var listHead = groups.Substring(0, groups.IndexOf("Members (", StringComparison.Ordinal));
        Assert.DoesNotContain("<th>Client</th>", listHead);
        Assert.Contains("<th>Client</th>", groups.Substring(groups.IndexOf("Members (", StringComparison.Ordinal)));
        var messages = Dot("Link.UI", "Views", "Operations", "_Messages.cshtml");
        Assert.DoesNotContain(">Summary</th>", messages);
        var facility = Dot("Link.UI", "Views", "Tenants", "_ViewReports.cshtml");
        Assert.DoesNotContain(">Measures</th>", facility);
        Assert.Contains("Census / IP", facility);
        Assert.Contains(".lu-reports-table > tbody > tr > td:nth-child(2) .lu-clip { max-width: none; }", Dot("Link.UI", "wwwroot", "css", "site.css"));
        Assert.Contains("<td class=\"lu-wrap\">@row.Name</td>", Dot("Link.UI", "Views", "Configuration", "Operations.cshtml"));
        Assert.Contains("<td class=\"lu-wrap\">@row.LongDescription</td>", Dot("Link.UI", "Views", "Configuration", "Hsloc.cshtml"));
    }
}
