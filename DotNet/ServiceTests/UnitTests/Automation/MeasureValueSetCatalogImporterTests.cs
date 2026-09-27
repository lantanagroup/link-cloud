using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class MeasureValueSetCatalogImporterTests
{
    [Fact]
    public void Hardcoded_seed_includes_story_pack_codes()
    {
        var items = GenerationCatalogSeed.FromHardcoded();
        Assert.Contains(items, i =>
            i.Kind == GenerationCatalogKind.Condition && i.Code == "233604007");
        Assert.Contains(items, i =>
            i.Kind == GenerationCatalogKind.Observation && i.Code == "2345-7");
        Assert.Contains(items, i =>
            i.Kind == GenerationCatalogKind.Medication && i.Code == "1116635");
    }

    [Fact]
    public void Hypo_bundle_imports_picker_types_and_diabetes_meds()
    {
        var json = ProfiledMeasureCatalog.ReadBundleJson(
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);
        var result = MeasureValueSetCatalogImporter.Import(json, "Hypo");

        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, i => i.Kind == GenerationCatalogKind.Observation);
        Assert.Contains(result.Items, i => i.Kind == GenerationCatalogKind.Medication);
        Assert.Contains(result.Items, i =>
            i.Kind == GenerationCatalogKind.Medication && i.Code == "1007184");
        Assert.True(
            result.DiabetesMedicationCodes.Count >= 2,
            $"expected diabetes medication codes, got {result.DiabetesMedicationCodes.Count}");
        Assert.All(result.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Code)));
        Assert.All(result.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.System)));
        Assert.DoesNotContain(result.Items, i =>
            string.Equals(i.Kind.ToString(), "Encounter", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i =>
            (i.SourceValueSet ?? "").Contains("1046.265", StringComparison.Ordinal)
            || (i.SourceValueSet ?? "").Contains("Location", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ach_daily_imports_membership_medications_and_lab_retrieves()
    {
        var json = ProfiledMeasureCatalog.ReadBundleJson(
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);
        var result = MeasureValueSetCatalogImporter.Import(json, "ACH Daily");

        Assert.Contains(result.Items, i => i.Kind == GenerationCatalogKind.Observation);
        Assert.Contains(result.Items, i => i.Kind == GenerationCatalogKind.Medication);
        Assert.Contains(result.Items, i => i.Kind == GenerationCatalogKind.ServiceRequest);
        Assert.Contains(result.Items, i => i.Kind == GenerationCatalogKind.Procedure);
        Assert.DoesNotContain(result.Items, i =>
            (i.SourceValueSet ?? "").Contains("Location", StringComparison.OrdinalIgnoreCase)
            || (i.SourceValueSet ?? "").Contains("discharge", StringComparison.OrdinalIgnoreCase)
            || (i.SourceValueSet ?? "").Contains("1046.274", StringComparison.Ordinal));
    }

    [Fact]
    public void Embedded_measures_import_more_than_the_hardcoded_tables()
    {
        var seed = GenerationCatalogSeed.FromHardcoded();
        var imported = MeasureValueSetCatalogImporter.ImportAllEmbeddedMeasures();
        Assert.True(
            imported.Items.Count > seed.Count,
            $"expected imported ({imported.Items.Count}) to exceed seed ({seed.Count})");
        Assert.True(
            imported.DiabetesMedicationCodes.Count >= 2,
            "diabetes medication value set should contribute codes");
    }
}
