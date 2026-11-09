
using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class FacilityTemplatesControllerTests
{
    private readonly FakeFacilityTemplateStore _store = new();
    private readonly Mock<IScenarioStore> _scenarioStore = new();

    private FacilityTemplatesController CreateController()
    {
        _scenarioStore
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestScenarioDefinition>());

        return new FacilityTemplatesController(
            _store,
            Mock.Of<IQueryPlanTemplateStore>(),
            Mock.Of<INormalizationStore>(),
            Mock.Of<IOrganizationResourceMapTemplateStore>(),
            Mock.Of<IPatientConfigurationStore>(),
            _scenarioStore.Object);
    }

    private static JsonElement GetBulkResponse(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task DeleteBulk_deletes_multiple_custom_templates()
    {
        var first = new FacilityTemplate { Name = "First" };
        var second = new FacilityTemplate { Name = "Second" };

        _store.Add(first);
        _store.Add(second);

        var controller = CreateController();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [first.Id, second.Id] },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        (await _store.GetByIdAsync(first.Id)).Should().BeNull();
        (await _store.GetByIdAsync(second.Id)).Should().BeNull();

        _scenarioStore.Verify(
            s => s.GetAllAsync(It.IsAny<CancellationToken>()),
            Times.Once);

    }

    [Fact]
    public async Task DeleteBulk_preserves_system_templates()
    {
        var system = new FacilityTemplate
        {
            Name = "System",
            IsSystem = true
        };

        _store.Add(system);

        var controller = CreateController();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [system.Id] },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        (await _store.GetByIdAsync(system.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteBulk_preserves_default_templates()
    {
        var template = new FacilityTemplate
        {
            Name = "Default",
            IsDefault = true
        };

        _store.Add(template);

        var controller = CreateController();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [template.Id] },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        (await _store.GetByIdAsync(template.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteBulk_rejects_empty_selection()
    {
        var controller = CreateController();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [] },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteBulk_rejects_empty_guid()
    {
        var controller = CreateController();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [Guid.Empty] },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteBulk_rejects_more_than_100_ids()
    {
        var controller = CreateController();

        var ids = Enumerable.Range(0, 101)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = ids },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteBulk_deduplicates_ids()
    {
        var template = new FacilityTemplate { Name = "Duplicate" };

        _store.Add(template);

        var controller = CreateController();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest
            {
                Ids = [template.Id, template.Id]
            },
            CancellationToken.None);

        var response = GetBulkResponse(result);

        var deletedIds = response.GetProperty("deletedIds")
            .EnumerateArray()
            .Select(id => id.GetGuid())
            .ToList();

        deletedIds.Should().ContainSingle().Which.Should().Be(template.Id);
        response.GetProperty("failures").GetArrayLength().Should().Be(0);

        (await _store.GetByIdAsync(template.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteBulk_preserves_referenced_template_and_reports_scenario()
    {
        var template = new FacilityTemplate { Name = "Referenced" };
        _store.Add(template);

        var controller = CreateController();

        _scenarioStore
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestScenarioDefinition>
            {
            new()
            {
                Name = "Bulk Delete Test",
                FacilityConfigurationMode = FacilityConfigurationMode.Facility,
                FacilityTemplateId = template.Id
            }
            });

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [template.Id] },
            CancellationToken.None);

        var response = GetBulkResponse(result);

        response.GetProperty("deletedIds").GetArrayLength().Should().Be(0);

        var failures = response.GetProperty("failures");
        failures.GetArrayLength().Should().Be(1);
        failures[0].GetProperty("id").GetGuid().Should().Be(template.Id);
        failures[0].GetProperty("reason").GetString()
            .Should().Contain("Bulk Delete Test");

        (await _store.GetByIdAsync(template.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteBulk_deletes_valid_template_and_reports_referenced_failure()
    {
        var deletable = new FacilityTemplate { Name = "Deletable" };
        var referenced = new FacilityTemplate { Name = "Referenced" };

        _store.Add(deletable);
        _store.Add(referenced);

        var controller = CreateController();

        _scenarioStore
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestScenarioDefinition>
            {
            new()
            {
                Name = "Protected Scenario",
                FacilityConfigurationMode = FacilityConfigurationMode.Facility,
                FacilityTemplateId = referenced.Id
            }
            });

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [deletable.Id, referenced.Id] },
            CancellationToken.None);

        var response = GetBulkResponse(result);

        var deletedIds = response.GetProperty("deletedIds")
            .EnumerateArray()
            .Select(id => id.GetGuid())
            .ToList();

        deletedIds.Should().ContainSingle().Which.Should().Be(deletable.Id);

        var failures = response.GetProperty("failures");
        failures.GetArrayLength().Should().Be(1);
        failures[0].GetProperty("id").GetGuid().Should().Be(referenced.Id);
        failures[0].GetProperty("reason").GetString()
            .Should().Contain("Protected Scenario");

        (await _store.GetByIdAsync(deletable.Id)).Should().BeNull();
        (await _store.GetByIdAsync(referenced.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteBulk_reports_unknown_id_without_stopping_other_deletions()
    {
        var existing = new FacilityTemplate { Name = "Existing" };
        var unknownId = Guid.NewGuid();

        _store.Add(existing);

        var controller = CreateController();

        var result = await controller.DeleteBulk(
            new BulkDeleteRequest { Ids = [unknownId, existing.Id] },
            CancellationToken.None);

        var response = GetBulkResponse(result);

        var deletedIds = response.GetProperty("deletedIds")
            .EnumerateArray()
            .Select(id => id.GetGuid())
            .ToList();

        deletedIds.Should().ContainSingle().Which.Should().Be(existing.Id);

        var failures = response.GetProperty("failures");
        failures.GetArrayLength().Should().Be(1);
        failures[0].GetProperty("id").GetGuid().Should().Be(unknownId);
        failures[0].GetProperty("reason").GetString()
            .Should().Contain("not found");

        (await _store.GetByIdAsync(existing.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteInline_still_deletes_custom_template()
    {
        var template = new FacilityTemplate { Name = "Single Delete" };
        _store.Add(template);

        var controller = CreateController();

        var result = await controller.DeleteInline(
            new IdRequest { Id = template.Id },
            CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        (await _store.GetByIdAsync(template.Id)).Should().BeNull();
    }

    private sealed class FakeFacilityTemplateStore : IFacilityTemplateStore
    {
        private readonly Dictionary<Guid, FacilityTemplate> _templates = [];

        public void Add(FacilityTemplate template)
        {
            _templates[template.Id] = template;
        }

        public Task<List<FacilityTemplate>> GetAllAsync(
            CancellationToken ct = default) =>
            Task.FromResult(_templates.Values.ToList());

        public Task<long> CountAsync(
            CancellationToken ct = default) =>
            Task.FromResult((long)_templates.Count);

        public Task<List<FacilityTemplate>> GetPageAsync(
            int skip,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult(_templates.Values
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Id)
                .Skip(skip)
                .Take(take)
                .ToList());

        public Task<FacilityTemplate?> GetByIdAsync(
            Guid id,
            CancellationToken ct = default) =>
            Task.FromResult(
                _templates.TryGetValue(id, out var template)
                    ? template
                    : null);

        public Task<FacilityTemplate?> GetDefaultAsync(
            CancellationToken ct = default) =>
            Task.FromResult(_templates.Values.FirstOrDefault(t => t.IsDefault));

        public Task UpsertAsync(
            FacilityTemplate template,
            CancellationToken ct = default)
        {
            Add(template);
            return Task.CompletedTask;
        }

        public Task SetDefaultAsync(
            Guid id,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            Guid id,
            CancellationToken ct = default)
        {
            _templates.Remove(id);
            return Task.CompletedTask;
        }
    }
}
