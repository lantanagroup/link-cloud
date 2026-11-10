using System.Text.RegularExpressions;
using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class CodeMapTargetSystemUiTests
{
    [Fact]
    public void Operation_editor_shows_hsloc_target_system_read_only_and_forces_it_on_save()
    {
        var editor = File.ReadAllText(FindRepoFile(Path.Combine(
            "DotNet", "Automation.UI", "Views", "Normalizations", "Index.cshtml")));
        var script = File.ReadAllText(FindRepoFile(Path.Combine(
            "DotNet", "Automation.UI", "wwwroot", "js", "code-map-target-system.js")));

        editor.Should().Contain("code-map-target-system.js");
        editor.Should().Contain("lockTargetSystemInput(");
        editor.Should().Contain("targetSystemForBuild(");
        editor.Should().Contain("normalizeCodeSystemMapsForSave(");
        editor.Should().Contain("hslocUrl: hslocTargetSystemUrl");
        editor.Should().Contain("Target system is the NHSN HSLOC URL and cannot be changed.");

        script.Should().Contain("input.readOnly = state.readOnly");
        script.Should().Contain("input.disabled = state.readOnly");
        script.Should().Contain("function lockTargetSystemInput");
        script.Should().Contain("dataset.codeMapTarget");
        script.Should().Contain("return hslocTargetSystemUrl");
        script.Should().Contain(MappingTargetSystems.HslocUrl);
    }

    [Fact]
    public void Js_hsloc_url_matches_MappingTargetSystems()
    {
        var script = File.ReadAllText(FindRepoFile(Path.Combine(
            "DotNet", "Automation.UI", "wwwroot", "js", "code-map-target-system.js")));
        var matches = Regex.Matches(script, @"hslocTargetSystemUrl = '([^']*)'");

        matches.Count.Should().Be(1);
        matches[0].Groups[1].Value.Should().Be(MappingTargetSystems.HslocUrl);
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
