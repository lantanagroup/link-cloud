using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

[Collection("IntegrationTests")]
public sealed class MongoFacilityTemplateStoreTests(
    AutomationUIIntegrationTestFixture fixture)
{
    private readonly AutomationUIIntegrationTestFixture _fixture = fixture;

    private static FacilityTemplate Template(
        string name,
        string? vendor = null,
        bool isSystem = false) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            VendorName = vendor,
            IsSystem = isSystem
        };

    [Fact]
    public async Task Filters_vendor_case_insensitively()
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("Epic Template", "Epic"));
        await store.UpsertAsync(Template("Cerner Template", "Cerner"));

        var results = await store.GetFilteredPageAsync(
            0, 10, null, "EPIC", null);

        results.Should().ContainSingle()
            .Which.Name.Should().Be("Epic Template");

        (await store.CountFilteredAsync(null, "epic", null))
            .Should().Be(1);
    }

    [Fact]
    public async Task Vendor_filter_requires_exact_match()
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("Epic", "Epic"));
        await store.UpsertAsync(Template("Epic Systems", "Epic Systems"));

        var results = await store.GetFilteredPageAsync(
            0, 10, null, "epic", null);

        results.Select(t => t.Name)
            .Should().Equal("Epic");
    }

    [Fact]
    public async Task Name_search_treats_regex_characters_as_literals()
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("A+B (Test)", "Epic"));
        await store.UpsertAsync(Template("AB Test", "Epic"));

        var results = await store.GetFilteredPageAsync(
            0, 10, "a+b (test)", null, null);

        results.Should().ContainSingle()
            .Which.Name.Should().Be("A+B (Test)");
    }

    [Fact]
    public async Task Filters_system_and_custom_templates()
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("System Template", isSystem: true));
        await store.UpsertAsync(Template("Custom Template"));

        var system = await store.GetFilteredPageAsync(
            0, 10, null, null, "System");

        var custom = await store.GetFilteredPageAsync(
            0, 10, null, null, "Custom");

        system.Select(t => t.Name)
            .Should().Equal("System Template");

        custom.Select(t => t.Name)
            .Should().Equal("Custom Template");
    }

    [Fact]
    public async Task Combines_search_vendor_and_kind_filters()
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("Epic Alpha", "Epic"));
        await store.UpsertAsync(Template("Epic Beta", "Epic", true));
        await store.UpsertAsync(Template("Epic Gamma", "Cerner"));

        var results = await store.GetFilteredPageAsync(
            0, 10, "epic", "EPIC", "Custom");

        results.Select(t => t.Name)
            .Should().Equal("Epic Alpha");

        (await store.CountFilteredAsync("epic", "EPIC", "Custom"))
            .Should().Be(1);
    }

    [Theory]
    [InlineData("name", "asc", "Alpha", "Bravo", "Zulu")]
    [InlineData("name", "desc", "Zulu", "Bravo", "Alpha")]
    [InlineData("vendor", "asc", "Alpha", "Zulu", "Bravo")]
    [InlineData("vendor", "desc", "Bravo", "Zulu", "Alpha")]
    [InlineData("kind", "asc", "Alpha", "Zulu", "Bravo")]
    [InlineData("kind", "desc", "Bravo", "Alpha", "Zulu")]
    public async Task Sorts_by_requested_column_and_direction(
        string sortBy,
        string sortDir,
        string first,
        string second,
        string third)
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("Zulu", "Beta"));
        await store.UpsertAsync(Template("Alpha", "Alpha"));
        await store.UpsertAsync(Template("Bravo", "Zulu", true));

        var results = await store.GetFilteredPageAsync(
            0, 10, null, null, null, sortBy, sortDir);

        results.Select(t => t.Name)
            .Should().Equal(first, second, third);
    }

    [Fact]
    public async Task Sorts_mixed_case_names_and_vendors_case_insensitively()
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("alpha", "Zulu"));
        await store.UpsertAsync(Template("zeta", "alpha"));
        await store.UpsertAsync(Template("Zulu", "zeta"));

        (await store.GetFilteredPageAsync(0, 10, null, null, null, "name", "asc"))
            .Select(t => t.Name).Should().Equal("alpha", "zeta", "Zulu");

        (await store.GetFilteredPageAsync(0, 10, null, null, null, "name", "desc"))
            .Select(t => t.Name).Should().Equal("Zulu", "zeta", "alpha");

        (await store.GetFilteredPageAsync(0, 10, null, null, null, "vendor", "asc"))
            .Select(t => t.Name).Should().Equal("zeta", "Zulu", "alpha");

        (await store.GetFilteredPageAsync(0, 10, null, null, null, "vendor", "desc"))
            .Select(t => t.Name).Should().Equal("alpha", "Zulu", "zeta");

        await _fixture.ResetAsync();
        store = _fixture.CreateFacilityTemplateStore();

        await store.UpsertAsync(Template("zeta", "Epic"));
        await store.UpsertAsync(Template("Zulu", "Epic"));
        await store.UpsertAsync(Template("alpha", "Epic"));

        (await store.GetFilteredPageAsync(0, 10, null, null, null, "vendor", "asc"))
            .Select(t => t.Name).Should().Equal("alpha", "zeta", "Zulu");
    }

    [Fact]
    public async Task Pagination_returns_stable_pages_with_full_documents()
    {
        await _fixture.ResetAsync();
        var store = _fixture.CreateFacilityTemplateStore();

        foreach (var name in new[] { "Delta", "Alpha", "Charlie", "Bravo" })
        {
            await store.UpsertAsync(new FacilityTemplate
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = $"Description for {name}"
            });
        }

        var firstPage = await store.GetFilteredPageAsync(
            0, 2, null, null, null);

        var secondPage = await store.GetFilteredPageAsync(
            2, 2, null, null, null);

        firstPage.Select(t => t.Name)
            .Should().Equal("Alpha", "Bravo");

        secondPage.Select(t => t.Name)
            .Should().Equal("Charlie", "Delta");

        firstPage.Concat(secondPage).Should().OnlyContain(
            t => t.Description == $"Description for {t.Name}");
    }
}
