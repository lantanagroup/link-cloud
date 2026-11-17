using System.Text.Json;
using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class TestRailCaseIdParserTests
{
    [Theory]
    [InlineData("123", 123)]
    [InlineData("C123", 123)]
    [InlineData("c123", 123)]
    [InlineData("  c0007 ", 7)]
    public void Accepts_positive_integer_and_c_prefix(string text, int expected)
    {
        TestRailCaseIdParser.TryParse(text, out var caseId).Should().BeTrue();
        caseId.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_is_optional(string? text)
    {
        TestRailCaseIdParser.TryParse(text, out var caseId).Should().BeTrue();
        caseId.Should().BeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("C0")]
    [InlineData("-1")]
    [InlineData("C-1")]
    [InlineData("abc")]
    [InlineData("C")]
    [InlineData("12.5")]
    [InlineData("C123x")]
    [InlineData("2147483648")]
    public void Rejects_zero_negative_and_junk(string text)
    {
        TestRailCaseIdParser.TryParse(text, out var caseId).Should().BeFalse();
        caseId.Should().BeNull();
    }
}

[Trait("Category", "UnitTests")]
public class TestRailCaseIdJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("""{"testRailCaseId":"C123"}""", 123)]
    [InlineData("""{"testRailCaseId":"123"}""", 123)]
    [InlineData("""{"testRailCaseId":123}""", 123)]
    [InlineData("""{"testRailCaseId":null}""", null)]
    [InlineData("""{"testRailCaseId":""}""", null)]
    public void Round_trips_accepted_forms(string json, int? expected)
    {
        var model = JsonSerializer.Deserialize<TestScenarioDefinition>(json, JsonOptions);

        model.Should().NotBeNull();
        model!.TestRailCaseId.Should().Be(expected);

        var written = JsonSerializer.Serialize(model, JsonOptions);
        using var doc = JsonDocument.Parse(written);
        if (expected is null)
            doc.RootElement.GetProperty("TestRailCaseId").ValueKind.Should().Be(JsonValueKind.Null);
        else
            doc.RootElement.GetProperty("TestRailCaseId").GetInt32().Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"testRailCaseId":"abc"}""")]
    [InlineData("""{"testRailCaseId":0}""")]
    [InlineData("""{"testRailCaseId":-4}""")]
    public void Rejects_invalid_json(string json)
    {
        var act = () => JsonSerializer.Deserialize<TestScenarioDefinition>(json, JsonOptions);
        act.Should().Throw<JsonException>();
    }
}

[Trait("Category", "UnitTests")]
public class ScenarioTestRailCaseIdApiTests
{
    [Fact]
    public async Task SaveInline_stores_a_positive_case_id()
    {
        var store = new Mock<IScenarioStore>();
        store.Setup(s => s.UpsertAsync(It.IsAny<TestScenarioDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateController(store.Object);
        var model = ValidScenario();
        model.TestRailCaseId = 123;

        var result = await controller.SaveInline(model, CancellationToken.None);

        result.Should().BeOfType<JsonResult>();
        store.Verify(s => s.UpsertAsync(
            It.Is<TestScenarioDefinition>(saved => saved.TestRailCaseId == 123 && saved.Name == "case-map"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task SaveInline_rejects_a_non_positive_case_id(int caseId)
    {
        var store = new Mock<IScenarioStore>();
        var controller = CreateController(store.Object);
        var model = ValidScenario();
        model.TestRailCaseId = caseId;

        var result = await controller.SaveInline(model, CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().Be("TestRail case id must be a positive integer.");
        store.Verify(s => s.UpsertAsync(It.IsAny<TestScenarioDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloneInline_copies_the_saved_case_id()
    {
        var sourceId = Guid.NewGuid();
        TestScenarioDefinition? saved = null;
        var store = new Mock<IScenarioStore>();
        store.Setup(s => s.GetByIdAsync(sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition
            {
                Id = sourceId,
                Name = "source",
                TestRailCaseId = 88
            });
        store.Setup(s => s.UpsertAsync(It.IsAny<TestScenarioDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<TestScenarioDefinition, CancellationToken>((clone, _) => saved = clone)
            .Returns(Task.CompletedTask);
        var controller = CreateController(store.Object);

        var result = await controller.CloneInline(new IdRequest { Id = sourceId }, CancellationToken.None);

        result.Should().BeOfType<JsonResult>();
        saved.Should().NotBeNull();
        saved!.Id.Should().NotBe(sourceId);
        saved.Name.Should().Be("source (Copy)");
        saved.TestRailCaseId.Should().Be(88);
    }

    [Fact]
    public void Scenario_editor_posts_and_loads_the_case_id_field()
    {
        var editor = File.ReadAllText(FindRepoFile(Path.Combine(
            "DotNet", "Automation.UI", "Views", "Shared", "_ScenarioEditorModal.cshtml")));
        var script = File.ReadAllText(FindRepoFile(Path.Combine(
            "DotNet", "Automation.UI", "wwwroot", "js", "testrail-case-id.js")));

        editor.Should().Contain("id=\"TestRailCaseId\"");
        editor.Should().Contain("testrail-case-id.js");
        editor.Should().Contain("parseTestRailCaseId(document.getElementById('TestRailCaseId').value)");
        editor.Should().Contain("data.testRailCaseId");
        script.Should().Contain("function parseTestRailCaseId");
    }

    private static TestScenarioDefinition ValidScenario() => new()
    {
        Name = "case-map",
        FacilityConfigurationMode = FacilityConfigurationMode.AlaCarte,
        QueryPlanTemplateId = Guid.NewGuid()
    };

    private static ScenariosController CreateController(IScenarioStore store)
    {
        var database = new Mock<IMongoDatabase>();
        database
            .Setup(d => d.GetCollection<ImportedBundleDocument>("automation_imported_bundles", null))
            .Returns(Mock.Of<IMongoCollection<ImportedBundleDocument>>());

        using var replacements = new PatientReplacementManager(
            Options.Create(new AutomationConfig()),
            NullLogger<PatientReplacementManager>.Instance);

        return new ScenariosController(
            store,
            Mock.Of<IQueryPlanTemplateStore>(),
            Mock.Of<INormalizationStore>(),
            Mock.Of<IOrganizationResourceMapTemplateStore>(),
            Mock.Of<IPatientConfigurationStore>(),
            Mock.Of<IMeasureTemplateStore>(),
            Mock.Of<IFacilityTemplateStore>(),
            Options.Create(new AutomationConfig()),
            database.Object,
            Mock.Of<IImportedBundleContentStore>(),
            replacements,
            NullLogger<ScenariosController>.Instance);
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
