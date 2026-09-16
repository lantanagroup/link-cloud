using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class HslocAutomationMapsTests
{
    [Fact]
    public void BuildCodeSystemMaps_WithoutPatients_IncludesRoleCodeMap()
    {
        var maps = HslocAutomationMaps.BuildCodeSystemMaps();

        maps.Should().ContainSingle(m =>
            m.SourceSystem == HslocMappingDefaults.RoleCodeSystem
            && m.TargetSystem == MappingTargetSystems.HslocUrl
            && m.CodeMaps["ICU"].Code == "1025-6");
        maps.Should().NotContain(m => m.SourceSystem == HslocMappingDefaults.IdentifierSystem);
    }

    [Fact]
    public void BuildCodeSystemMaps_WithGeneratedPatients_AddsIdentifierMap()
    {
        var maps = HslocAutomationMaps.BuildCodeSystemMaps(["Patient-abcd1234-001"]);

        var identifier = maps.Single(m => m.SourceSystem == HslocMappingDefaults.IdentifierSystem);
        identifier.CodeMaps["abcd1234-Loc-ICU"].Code.Should().Be("1025-6");
        identifier.CodeMaps["abcd1234-Loc-Hospital"].Code.Should().Be("1060-3");
    }

    [Fact]
    public void Merge_AddsGeneratedIdentifierMapsToExistingRoleMaps()
    {
        var existing = HslocAutomationMaps.BuildCodeSystemMaps();
        var merged = HslocAutomationMaps.Merge(existing, ["Patient-abcd1234-001"]);

        merged.Should().Contain(m => m.SourceSystem == HslocMappingDefaults.RoleCodeSystem);
        merged.Should().Contain(m =>
            m.SourceSystem == HslocMappingDefaults.IdentifierSystem
            && m.CodeMaps.ContainsKey("abcd1234-Loc-ED"));
    }

    [Fact]
    public void Merge_DoesNotOverwriteConfiguredRoleCodeMaps()
    {
        var existing = new List<NormalizationCodeSystemMap>
        {
            new()
            {
                SourceSystem = HslocMappingDefaults.RoleCodeSystem,
                TargetSystem = MappingTargetSystems.HslocUrl,
                CodeMaps = { ["ICU"] = new NormalizationCodeMapEntry { Code = "9999-9", Display = "Custom ICU" } }
            }
        };

        var merged = HslocAutomationMaps.Merge(existing, ["Patient-abcd1234-001"]);

        merged.Should().ContainSingle(m => m.SourceSystem == HslocMappingDefaults.RoleCodeSystem)
            .Which.CodeMaps["ICU"].Code.Should().Be("9999-9");
        merged.Should().Contain(m => m.SourceSystem == HslocMappingDefaults.IdentifierSystem);
    }

    [Fact]
    public void Merge_DoesNotOverwriteConfiguredIdentifierKeys()
    {
        var existing = new List<NormalizationCodeSystemMap>
        {
            new()
            {
                SourceSystem = HslocMappingDefaults.IdentifierSystem,
                TargetSystem = MappingTargetSystems.HslocUrl,
                CodeMaps = { ["abcd1234-Loc-ICU"] = new NormalizationCodeMapEntry { Code = "1108-0", Display = "Emergency Department" } }
            }
        };

        var merged = HslocAutomationMaps.Merge(existing, ["Patient-abcd1234-001"]);
        var identifier = merged.Single(m => m.SourceSystem == HslocMappingDefaults.IdentifierSystem);
        identifier.CodeMaps["abcd1234-Loc-ICU"].Code.Should().Be("1108-0");
        identifier.CodeMaps["abcd1234-Loc-Hospital"].Code.Should().Be("1060-3");
    }
}
