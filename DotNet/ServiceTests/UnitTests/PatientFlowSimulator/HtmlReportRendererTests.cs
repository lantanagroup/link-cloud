using System.Text.RegularExpressions;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Reporting;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class HtmlReportRendererTests
{
    private static readonly YearMonth November = new(2026, 11);

    [Fact]
    public void Render_NovemberReport_KeepsEveryChartAndTableAndLoadsMermaid()
    {
        var scenario = DefaultScenario.Load();
        var markdown = new PatientFlowReportBuilder(scenario).Build(November);
        var charts = Regex.Matches(markdown, "^```mermaid\r?$", RegexOptions.Multiline).Count;

        var html = HtmlReportRenderer.Render(markdown, "Patient flow 2026-11");

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<title>Patient flow 2026-11</title>", html);
        Assert.Contains(HtmlReportRenderer.MermaidScriptUrl, html);
        Assert.Equal(charts, Regex.Matches(html, "<pre class=\"mermaid\">").Count);
        Assert.Contains("<table>", html);
        Assert.Contains("<h2 id=", html);
    }

    [Fact]
    public void Render_Title_IsHtmlEncoded()
    {
        var html = HtmlReportRenderer.Render("# x", "a <b> & c");

        Assert.Contains("<title>a &lt;b&gt; &amp; c</title>", html);
    }
}
