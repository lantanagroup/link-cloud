using FluentAssertions;
using Link.UI.Controllers;
using Xunit;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Tests;

public class AutomationConfigurationRouteTests
{
    [Theory]
    [InlineData(typeof(AutomationConfigurationsController), "Automation/Configurations")]
    [InlineData(typeof(FacilityTemplatesController), "Automation/FacilityTemplates/[action]")]
    [InlineData(typeof(MeasuresController), "Automation/Measures/[action]")]
    [InlineData(typeof(QueryPlansController), "Automation/QueryPlans/[action]")]
    [InlineData(typeof(NormalizationsController), "Automation/Normalizations/[action]")]
    [InlineData(typeof(OrganizationResourceMapsController), "Automation/OrganizationResourceMaps/[action]")]
    [InlineData(typeof(PatientConfigurationsController), "Automation/PatientConfigurations/[action]")]
    public void Configuration_editors_are_link_ui_routes(Type controller, string route)
    {
        var attribute = controller.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();
        attribute.Template.Should().Be(route);
    }

    [Theory]
    [InlineData("DotNet/Link.UI/Views/Measures/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/Normalizations/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/OrganizationResourceMaps/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/QueryPlans/Index.cshtml")]
    public void Editor_deep_link_runs_after_the_page_script(string relativePath)
    {
        var text = File.ReadAllText(RepoFile(relativePath));
        var section = text.IndexOf("@section Scripts", StringComparison.Ordinal);
        var script = text.IndexOf("configuration-deep-link.js", StringComparison.Ordinal);
        var close = text.LastIndexOf('}');
        section.Should().BeGreaterThanOrEqualTo(0);
        script.Should().BeGreaterThan(section);
        script.Should().BeLessThan(close);
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
