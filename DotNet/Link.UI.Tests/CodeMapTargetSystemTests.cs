using System.Text.RegularExpressions;
using Automation.UI.Models;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Shared.Application.Utilities;
using Link.UI.Controllers;
using Xunit;

namespace Link.UI.Tests;

public class CodeMapTargetSystemTests
{
    [Fact]
    public void Operation_editor_shows_the_hsloc_target_read_only_and_sends_the_nhsn_url()
    {
        var editor = File.ReadAllText(Repo("DotNet/Link.UI/Views/Normalizations/Index.cshtml"));
        var script = File.ReadAllText(Repo("DotNet/Link.UI/wwwroot/js/code-map-target-system.js"));

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

        var matches = Regex.Matches(script, @"hslocTargetSystemUrl = '([^']*)'");
        matches.Count.Should().Be(1);
        matches[0].Groups[1].Value.Should().Be(MappingTargetSystems.HslocUrl);
    }

    [Fact]
    public void Save_still_rejects_an_hsloc_map_whose_target_is_not_the_nhsn_url()
    {
        var error = NormalizationsController.GetHslocMapSaveError(new NormalizationOperationDefinition
        {
            OperationType = HslocMappingDefaults.OperationType,
            ResourceTypes = ["Location"],
            CodeMapFhirPath = HslocMappingDefaults.FhirPath,
            CodeSystemMaps =
            [
                new NormalizationCodeSystemMap
                {
                    SourceSystem = "https://example.test/local",
                    TargetSystem = "https://example.test/other"
                }
            ]
        });

        error.Should().Contain(MappingTargetSystems.HslocUrl);

        NormalizationsController.GetHslocMapSaveError(new NormalizationOperationDefinition
        {
            OperationType = HslocMappingDefaults.OperationType,
            ResourceTypes = ["Location"],
            CodeMapFhirPath = HslocMappingDefaults.FhirPath,
            CodeSystemMaps =
            [
                new NormalizationCodeSystemMap
                {
                    SourceSystem = "https://example.test/local",
                    TargetSystem = MappingTargetSystems.HslocUrl
                }
            ]
        }).Should().BeNull();
    }

    private static string Repo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
