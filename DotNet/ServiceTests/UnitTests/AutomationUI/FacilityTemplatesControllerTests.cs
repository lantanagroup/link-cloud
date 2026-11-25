
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

    [Fact]
    public async Task GetFilteredPage_sorts_by_name_descending()
    {
        _store.Add(new FacilityTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Alpha"
        });

        _store.Add(new FacilityTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Zulu"
        });

        _store.Add(new FacilityTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Bravo"
        });

        var results = await _store.GetFilteredPageAsync(
            0,
            10,
            null,
            null,
            null,
            "name",
            "desc");

        results.Select(t => t.Name)
            .Should()
            .Equal("Zulu", "Bravo", "Alpha");
    }

    [Theory]
    [InlineData("name", "asc", "name", "asc")]
    [InlineData("name", "desc", "name", "desc")]
    [InlineData("vendor", "asc", "vendor", "asc")]
    [InlineData("vendor", "desc", "vendor", "desc")]
    [InlineData("kind", "asc", "kind", "asc")]
    [InlineData("kind", "desc", "kind", "desc")]
    [InlineData("VENDOR", "DESC", "vendor", "desc")]
    [InlineData("invalid", "invalid", "name", "asc")]
    public async Task Index_normalizes_and_forwards_sorting(
        string sortBy,
        string sortDir,
        string expectedSortBy,
        string expectedSortDir)
    {
        var controller = CreateController();

        var result = await controller.Index(
            sortBy: sortBy,
            sortDir: sortDir);

        result.Should().BeOfType<ViewResult>();

        _store.LastSortBy.Should().Be(expectedSortBy);
        _store.LastSortDir.Should().Be(expectedSortDir);

        ((string)controller.ViewBag.SortBy).Should().Be(expectedSortBy);
        ((string)controller.ViewBag.SortDir).Should().Be(expectedSortDir);
    }

    [Theory]
    [InlineData("custom", "Custom")]
    [InlineData("CUSTOM", "Custom")]
    [InlineData("CuStOm", "Custom")]
    [InlineData("system", "System")]
    [InlineData("SYSTEM", "System")]
    [InlineData("SyStEm", "System")]
    [InlineData("invalid", null)]
    public async Task Index_normalizes_kind_filter(
        string kind,
        string? expectedKind)
    {
        var controller = CreateController();

        var result = await controller.Index(kind: kind);

        result.Should().BeOfType<ViewResult>();

        _store.LastKind.Should().Be(expectedKind);
        ((string?)controller.ViewBag.Kind).Should().Be(expectedKind);
    }

    [Fact]
    public async Task Index_forwards_trimmed_search_and_vendor()
    {
        var controller = CreateController();

        var result = await controller.Index(
            search: "  Epic  ",
            vendor: "  Cerner  ",
            kind: "custom");

        result.Should().BeOfType<ViewResult>();

        _store.LastSearch.Should().Be("Epic");
        _store.LastVendor.Should().Be("Cerner");
        _store.LastKind.Should().Be("Custom");

        ((string?)controller.ViewBag.Search).Should().Be("Epic");
        ((string?)controller.ViewBag.Vendor).Should().Be("Cerner");
    }

    [Theory]
    [InlineData(0, 10, 1, 0, 10)]
    [InlineData(-1, 10, 1, 0, 10)]
    [InlineData(2, 10, 2, 10, 10)]
    [InlineData(99, 10, 3, 20, 10)]
    [InlineData(1, 15, 1, 0, 25)]
    public async Task Index_clamps_page_and_validates_page_size(
        int page,
        int pageSize,
        int expectedPage,
        int expectedSkip,
        int expectedTake)
    {
        for (var i = 0; i < 25; i++)
        {
            _store.Add(new FacilityTemplate
            {
                Id = Guid.NewGuid(),
                Name = $"Template {i:D2}"
            });
        }

        var controller = CreateController();

        var result = await controller.Index(
            page: page,
            pageSize: pageSize);

        result.Should().BeOfType<ViewResult>();

        _store.LastSkip.Should().Be(expectedSkip);
        _store.LastTake.Should().Be(expectedTake);

        ((int)controller.ViewBag.CurrentPage).Should().Be(expectedPage);
        ((int)controller.ViewBag.PageSize).Should().Be(expectedTake);
    }


    [Fact]
    public async Task Index_preserves_special_characters_in_search()
    {
        var controller = CreateController();

        var result = await controller.Index(
            search: "  A & B's Template  ");

        result.Should().BeOfType<ViewResult>();

        _store.LastSearch.Should().Be("A & B's Template");
        ((string?)controller.ViewBag.Search)
            .Should().Be("A & B's Template");
    }

    [Fact]
    public async Task Index_limits_search_and_vendor_length()
    {
        var controller = CreateController();

        var result = await controller.Index(
            search: new string('A', 250),
            vendor: new string('B', 250));

        result.Should().BeOfType<ViewResult>();

        _store.LastSearch.Should().HaveLength(200);
        _store.LastVendor.Should().HaveLength(200);
    }

    [Theory]
    [InlineData("epic")]
    [InlineData("EPIC")]
    [InlineData("EpIc")]
    public async Task GetFilteredPage_filters_vendor_case_insensitively(
        string vendor)
    {
        _store.Add(new FacilityTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Epic Template",
            VendorName = "Epic"
        });

        _store.Add(new FacilityTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Cerner Template",
            VendorName = "Cerner"
        });

        var results = await _store.GetFilteredPageAsync(
            0, 10, null, vendor, null);

        results.Should().ContainSingle()
            .Which.Name.Should().Be("Epic Template");
    }

    private sealed class FakeFacilityTemplateStore : IFacilityTemplateStore
    {
        private readonly Dictionary<Guid, FacilityTemplate> _templates = [];
        public int LastSkip { get; private set; }
        public int LastTake { get; private set; }
        public string? LastSearch { get; private set; }
        public string? LastVendor { get; private set; }
        public string? LastKind { get; private set; }
        public string? LastSortBy { get; private set; }
        public string? LastSortDir { get; private set; }

        public void Add(FacilityTemplate template)
        {
            _templates[template.Id] = template;
        }

        public Task<List<FacilityTemplate>> GetAllAsync(
            CancellationToken ct = default) =>
            Task.FromResult(_templates.Values.ToList());

        public Task<long> CountFilteredAsync(
            string? search,
            string? vendor,
            string? kind,
            CancellationToken ct = default) =>
            Task.FromResult((long)FilterTemplates(search, vendor, kind).Count());

        public Task<List<FacilityTemplate>> GetFilteredPageAsync(
            int skip,
            int take,
            string? search,
            string? vendor,
            string? kind,
            string sortBy = "name",
            string sortDir = "asc",
            CancellationToken ct = default)
        {

            LastSkip = skip;
            LastTake = take;
            LastSearch = search;
            LastVendor = vendor;
            LastKind = kind;
            LastSortBy = sortBy;
            LastSortDir = sortDir;

            var filtered = FilterTemplates(search, vendor, kind);

            var descending = string.Equals(
                sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            IOrderedEnumerable<FacilityTemplate> sorted =
                sortBy.ToLowerInvariant() switch
                {
                    "vendor" => descending
                        ? filtered.OrderByDescending(t => t.VendorName)
                        : filtered.OrderBy(t => t.VendorName),

                    "kind" => descending
                        ? filtered.OrderByDescending(t => t.IsSystem)
                        : filtered.OrderBy(t => t.IsSystem),

                    _ => descending
                        ? filtered.OrderByDescending(t => t.Name)
                        : filtered.OrderBy(t => t.Name)
                };

            var ordered = sortBy.Equals("name", StringComparison.OrdinalIgnoreCase)
                ? sorted.ThenBy(t => t.Id)
                : sorted.ThenBy(t => t.Name).ThenBy(t => t.Id);

            return Task.FromResult(ordered
                .Skip(skip)
                .Take(take)
                .ToList());
        }

        private IEnumerable<FacilityTemplate> FilterTemplates(
            string? search,
            string? vendor,
            string? kind)
        {
            return _templates.Values.Where(t =>
                (string.IsNullOrWhiteSpace(search) ||
                 t.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) &&

                (string.IsNullOrWhiteSpace(vendor) ||
                 string.Equals(t.VendorName, vendor.Trim(), StringComparison.OrdinalIgnoreCase)) &&

                (string.IsNullOrWhiteSpace(kind) ||
                 (kind.Equals("System", StringComparison.OrdinalIgnoreCase) && t.IsSystem) ||
                 (kind.Equals("Custom", StringComparison.OrdinalIgnoreCase) && !t.IsSystem) ||
                 (!kind.Equals("System", StringComparison.OrdinalIgnoreCase) &&
                  !kind.Equals("Custom", StringComparison.OrdinalIgnoreCase))));
        }

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
