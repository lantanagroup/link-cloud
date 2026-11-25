using System.Reflection;
using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Link.UI.Controllers;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Link.UI.Tests;

public class FacilityTemplatesControllerTests
{
    [Fact]
    public async Task Index_uses_the_default_page_and_clamps_a_page_past_the_end()
    {
        var templates = new FakeTemplates();
        for (var i = 0; i < 30; i++)
            templates.Items.Add(Template("T" + i.ToString("00")));
        var sut = Ready(templates, new FakeScenarios());

        var first = await sut.Index(ct: CancellationToken.None);
        var firstPage = first.Should().BeOfType<ViewResult>().Subject.Model.Should().BeAssignableTo<List<FacilityTemplate>>().Subject;
        firstPage.Should().HaveCount(FacilityTemplateListRules.DefaultPageSize);
        templates.Pages.Should().Equal((0, FacilityTemplateListRules.DefaultPageSize));
        ((int)sut.ViewBag.CurrentPage).Should().Be(1);
        ((int)sut.ViewBag.PageSize).Should().Be(25);
        ((long)sut.ViewBag.TotalCount).Should().Be(30);
        ((int)sut.ViewBag.TotalPages).Should().Be(2);

        var last = await sut.Index(page: 9, pageSize: 25, ct: CancellationToken.None);
        var lastPage = last.Should().BeOfType<ViewResult>().Subject.Model.Should().BeAssignableTo<List<FacilityTemplate>>().Subject;
        lastPage.Should().HaveCount(5);
        lastPage.Select(t => t.Name).Should().Equal("T25", "T26", "T27", "T28", "T29");
        templates.Pages.Should().Equal((0, 25), (25, 25));
        ((int)sut.ViewBag.CurrentPage).Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(1000)]
    public async Task Index_replaces_an_unknown_page_size_with_the_default(int pageSize)
    {
        var templates = new FakeTemplates();
        templates.Items.Add(Template("Only"));
        var sut = Ready(templates, new FakeScenarios());

        await sut.Index(page: -3, pageSize: pageSize, ct: CancellationToken.None);

        templates.Pages.Should().Equal((0, FacilityTemplateListRules.DefaultPageSize));
        ((int)sut.ViewBag.CurrentPage).Should().Be(1);
        ((int)sut.ViewBag.PageSize).Should().Be(FacilityTemplateListRules.DefaultPageSize);
        ((int)sut.ViewBag.TotalPages).Should().Be(1);
    }

    [Fact]
    public async Task Index_asks_for_the_requested_page_size_and_an_empty_list_stays_on_page_one()
    {
        var sized = new FakeTemplates();
        for (var i = 0; i < 12; i++)
            sized.Items.Add(Template("P" + i.ToString("00")));
        var sizedController = Ready(sized, new FakeScenarios());
        var page = await sizedController.Index(page: 2, pageSize: 10, ct: CancellationToken.None);
        page.Should().BeOfType<ViewResult>().Subject.Model.Should().BeAssignableTo<List<FacilityTemplate>>().Subject.Should().HaveCount(2);
        sized.Pages.Should().Equal((10, 10));

        var empty = new FakeTemplates();
        var emptyController = Ready(empty, new FakeScenarios());
        var none = await emptyController.Index(page: 4, pageSize: 50, ct: CancellationToken.None);
        none.Should().BeOfType<ViewResult>().Subject.Model.Should().BeAssignableTo<List<FacilityTemplate>>().Subject.Should().BeEmpty();
        empty.Pages.Should().Equal((0, 50));
        ((int)emptyController.ViewBag.CurrentPage).Should().Be(1);
        ((long)emptyController.ViewBag.TotalCount).Should().Be(0);
        ((int)emptyController.ViewBag.TotalPages).Should().Be(1);
    }

    [Fact]
    public async Task Index_forwards_cancellation()
    {
        var templates = new FakeTemplates();
        var sut = Ready(templates, new FakeScenarios());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.Index(ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        templates.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task Index_counts_and_pages_only_the_filtered_templates()
    {
        var templates = new FakeTemplates();
        for (var i = 0; i < 12; i++)
            templates.Items.Add(Template("Epic " + i.ToString("00"), vendor: "Epic"));
        for (var i = 0; i < 8; i++)
            templates.Items.Add(Template("Cerner " + i.ToString("00"), vendor: "Cerner"));
        var sut = Ready(templates, new FakeScenarios());

        var result = await sut.Index(page: 9, pageSize: 10, search: "  epic  ", vendor: "  EPIC  ", kind: "custom", ct: CancellationToken.None);
        var page = result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeAssignableTo<List<FacilityTemplate>>().Subject;

        templates.LastSearch.Should().Be("epic");
        templates.LastVendor.Should().Be("EPIC");
        templates.LastKind.Should().Be("Custom");
        ((long)sut.ViewBag.TotalCount).Should().Be(12);
        ((int)sut.ViewBag.TotalPages).Should().Be(2);
        ((int)sut.ViewBag.CurrentPage).Should().Be(2);
        ((int)sut.ViewBag.PageSize).Should().Be(10);
        templates.Pages.Should().Equal((10, 10));
        page.Select(t => t.Name).Should().Equal("Epic 10", "Epic 11");
    }

    [Fact]
    public async Task Index_treats_blank_and_unknown_filters_as_the_full_set()
    {
        var templates = new FakeTemplates();
        templates.Items.Add(Template("Alpha", vendor: "Epic", system: true));
        templates.Items.Add(Template("Beta", vendor: "Cerner"));
        var sut = Ready(templates, new FakeScenarios());

        var result = await sut.Index(
            page: 4,
            search: "   ",
            vendor: " ",
            kind: "neither",
            sortBy: "nope",
            sortDir: "sideways",
            ct: CancellationToken.None);

        result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeAssignableTo<List<FacilityTemplate>>().Subject.Should().HaveCount(2);
        templates.LastSearch.Should().BeEmpty();
        templates.LastVendor.Should().BeEmpty();
        templates.LastKind.Should().BeNull();
        templates.LastSortBy.Should().Be("name");
        templates.LastSortDir.Should().Be("asc");
        ((long)sut.ViewBag.TotalCount).Should().Be(2);
        ((int)sut.ViewBag.CurrentPage).Should().Be(1);
        ((int)sut.ViewBag.TotalPages).Should().Be(1);
        templates.Pages.Should().Equal((0, FacilityTemplateListRules.DefaultPageSize));
    }

    [Fact]
    public async Task Index_passes_sort_and_limits_filter_length()
    {
        var templates = new FakeTemplates();
        templates.Items.Add(Template("A", vendor: "Zulu"));
        templates.Items.Add(Template("B", vendor: "Alpha"));
        templates.Items.Add(Template("C", vendor: "Middle", system: true));
        var sut = Ready(templates, new FakeScenarios());

        var search = new string('n', 250);
        var vendor = new string('v', 250);
        await sut.Index(search: search, vendor: vendor, kind: "SYSTEM", sortBy: "VENDOR", sortDir: "DESC", ct: CancellationToken.None);

        templates.LastSearch.Should().HaveLength(200).And.Be(new string('n', 200));
        templates.LastVendor.Should().HaveLength(200).And.Be(new string('v', 200));
        templates.LastKind.Should().Be("System");
        templates.LastSortBy.Should().Be("vendor");
        templates.LastSortDir.Should().Be("desc");
        ((string)sut.ViewBag.SortBy).Should().Be("vendor");
        ((string)sut.ViewBag.SortDir).Should().Be("desc");
        ((string?)sut.ViewBag.Kind).Should().Be("System");
    }

    [Fact]
    public async Task Index_returns_an_empty_page_when_nothing_matches()
    {
        var templates = new FakeTemplates();
        templates.Items.Add(Template("Alpha", vendor: "Epic"));
        var sut = Ready(templates, new FakeScenarios());

        var result = await sut.Index(page: 2, search: "missing", kind: "custom", ct: CancellationToken.None);
        var page = result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeAssignableTo<List<FacilityTemplate>>().Subject;

        page.Should().BeEmpty();
        ((long)sut.ViewBag.TotalCount).Should().Be(0);
        ((int)sut.ViewBag.CurrentPage).Should().Be(1);
        ((int)sut.ViewBag.TotalPages).Should().Be(1);
        templates.Pages.Should().Equal((0, FacilityTemplateListRules.DefaultPageSize));
    }

    [Fact]
    public async Task DeleteBulk_deletes_the_selected_custom_templates()
    {
        var first = Template("Alpha");
        var second = Template("Beta");
        var templates = new FakeTemplates { Items = { first, second } };
        var scenarios = new FakeScenarios();
        var sut = Ready(templates, scenarios);

        var result = await sut.DeleteBulk(new BulkDeleteRequest { Ids = [first.Id, second.Id] }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        DeletedIds(ok.Value!).Should().Equal(first.Id, second.Id);
        Failures(ok.Value!).Should().BeEmpty();
        templates.Items.Should().BeEmpty();
        scenarios.Reads.Should().Be(1);
    }

    [Fact]
    public async Task DeleteBulk_keeps_system_and_default_templates_and_reports_why()
    {
        var custom = Template("Custom");
        var system = Template("System", system: true);
        var fallback = Template("Fallback", isDefault: true);
        var templates = new FakeTemplates { Items = { custom, system, fallback } };
        var sut = Ready(templates, new FakeScenarios());

        var result = await sut.DeleteBulk(new BulkDeleteRequest { Ids = [custom.Id, system.Id, fallback.Id] }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        DeletedIds(ok.Value!).Should().Equal(custom.Id);
        Failures(ok.Value!).Should().Equal(
            (system.Id, "System template cannot be deleted."),
            (fallback.Id, "The system facility config cannot be deleted."));
        templates.Items.Select(t => t.Id).Should().Equal(system.Id, fallback.Id);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task DeleteBulk_rejects_an_empty_selection_or_an_empty_id(bool emptyBody, bool emptyId)
    {
        var templates = new FakeTemplates { Items = { Template("Kept") } };
        var scenarios = new FakeScenarios();
        var sut = Ready(templates, scenarios);
        BulkDeleteRequest? request = emptyBody
            ? null
            : new BulkDeleteRequest { Ids = emptyId ? [Guid.Empty] : [] };

        var result = await sut.DeleteBulk(request, CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().Be(emptyId
            ? "All template IDs must be non-empty GUIDs."
            : "Select at least one facility template.");
        templates.Deleted.Should().BeEmpty();
        scenarios.Reads.Should().Be(0);
    }

    [Fact]
    public async Task DeleteBulk_rejects_more_than_the_maximum_and_duplicate_ids_delete_once()
    {
        var kept = Template("Kept");
        var templates = new FakeTemplates { Items = { kept } };
        var scenarios = new FakeScenarios();
        var sut = Ready(templates, scenarios);
        var tooMany = Enumerable.Range(0, FacilityTemplateListRules.MaxBulkDelete + 1).Select(_ => Guid.NewGuid()).ToList();

        var rejected = await sut.DeleteBulk(new BulkDeleteRequest { Ids = tooMany }, CancellationToken.None);

        rejected.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().Be(
            "A maximum of 100 templates can be deleted at once.");
        scenarios.Reads.Should().Be(0);

        var once = await sut.DeleteBulk(new BulkDeleteRequest { Ids = [kept.Id, kept.Id] }, CancellationToken.None);
        DeletedIds(once.Should().BeOfType<OkObjectResult>().Subject.Value!).Should().Equal(kept.Id);
        templates.Deleted.Should().Equal(kept.Id);
        scenarios.Reads.Should().Be(1);
    }

    [Fact]
    public async Task DeleteBulk_reports_a_template_that_is_in_use_and_still_deletes_the_others()
    {
        var used = Template("Used");
        var free = Template("Free");
        var missing = Guid.NewGuid();
        var templates = new FakeTemplates { Items = { used, free } };
        var scenarios = new FakeScenarios
        {
            Items =
            {
                new TestScenarioDefinition
                {
                    Name = "Morning census",
                    FacilityConfigurationMode = FacilityConfigurationMode.Facility,
                    FacilityTemplateId = used.Id
                },
                new TestScenarioDefinition
                {
                    Name = "Evening census",
                    FacilityConfigurationMode = FacilityConfigurationMode.Facility,
                    FacilityTemplateId = used.Id
                },
                new TestScenarioDefinition
                {
                    Name = "Ala carte",
                    FacilityConfigurationMode = FacilityConfigurationMode.AlaCarte,
                    FacilityTemplateId = free.Id
                }
            }
        };
        var sut = Ready(templates, scenarios);

        var result = await sut.DeleteBulk(new BulkDeleteRequest { Ids = [used.Id, missing, free.Id] }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        DeletedIds(ok.Value!).Should().Equal(free.Id);
        Failures(ok.Value!).Should().Equal(
            (used.Id, "This facility template is used by Morning census, Evening census. Remove it from those scenarios before deleting it."),
            (missing, "Facility template was not found."));
        templates.Items.Should().ContainSingle(t => t.Id == used.Id);
        scenarios.Reads.Should().Be(1);
    }

    [Fact]
    public async Task DeleteBulk_turns_an_unexpected_delete_error_into_a_per_template_failure()
    {
        var broken = Template("Broken");
        var fine = Template("Fine");
        var templates = new FakeTemplates
        {
            Items = { broken, fine },
            DeleteError = new InvalidOperationException("store blew up"),
            DeleteErrorId = broken.Id
        };
        var sut = Ready(templates, new FakeScenarios());

        var result = await sut.DeleteBulk(new BulkDeleteRequest { Ids = [broken.Id, fine.Id] }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        DeletedIds(ok.Value!).Should().Equal(fine.Id);
        Failures(ok.Value!).Should().Equal((broken.Id, "An unexpected error occurred while deleting this template."));
        templates.Items.Should().ContainSingle(t => t.Id == broken.Id);
    }

    [Fact]
    public async Task DeleteBulk_rethrows_cancellation_instead_of_recording_a_failure()
    {
        var template = Template("One");
        using var cts = new CancellationTokenSource();
        var templates = new FakeTemplates
        {
            Items = { template },
            CancelDuringDelete = cts
        };
        var sut = Ready(templates, new FakeScenarios());

        var act = async () => await sut.DeleteBulk(new BulkDeleteRequest { Ids = [template.Id] }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        templates.Items.Should().ContainSingle();
        templates.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteInline_still_deletes_a_custom_template_and_protects_the_others()
    {
        var custom = Template("Custom");
        var system = Template("System", system: true);
        var fallback = Template("Fallback", isDefault: true);
        var used = Template("Used");
        var templates = new FakeTemplates { Items = { custom, system, fallback, used } };
        var scenarios = new FakeScenarios
        {
            Items =
            {
                new TestScenarioDefinition
                {
                    Name = "Linked",
                    FacilityConfigurationMode = FacilityConfigurationMode.Facility,
                    FacilityTemplateId = used.Id
                }
            }
        };
        var sut = Ready(templates, scenarios);

        (await sut.DeleteInline(new IdRequest { Id = custom.Id }, CancellationToken.None)).Should().BeOfType<OkResult>();
        templates.Items.Should().NotContain(t => t.Id == custom.Id);

        var forbidden = await sut.DeleteInline(new IdRequest { Id = system.Id }, CancellationToken.None);
        forbidden.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        forbidden.Should().BeOfType<ObjectResult>().Subject.Value.Should().Be("System template cannot be deleted.");

        var blocked = await sut.DeleteInline(new IdRequest { Id = fallback.Id }, CancellationToken.None);
        blocked.Should().BeOfType<ConflictObjectResult>().Subject.Value.Should().Be("The system facility config cannot be deleted.");

        var inUse = await sut.DeleteInline(new IdRequest { Id = used.Id }, CancellationToken.None);
        inUse.Should().BeOfType<ConflictObjectResult>().Subject.Value.Should().Be(
            "This facility template is used by Linked. Remove it from those scenarios before deleting it.");

        (await sut.DeleteInline(new IdRequest { Id = Guid.NewGuid() }, CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        templates.Items.Select(t => t.Id).Should().Equal(system.Id, fallback.Id, used.Id);
    }

    [Fact]
    public void Requests_stop_when_the_engine_is_off()
    {
        var (sut, context) = Build(new FakeTemplates(), new FakeScenarios(), ready: false);

        var content = context.Result.Should().BeOfType<ContentResult>().Subject;
        content.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        content.Content.Should().Be("Engine off.");
        sut.Should().NotBeNull();
    }

    [Fact]
    public void Paging_and_bulk_delete_follow_the_page_rules_and_the_existing_confirm()
    {
        FacilityTemplateListRules.DefaultPageSize.Should().Be(25);
        FacilityTemplateListRules.MaxBulkDelete.Should().Be(100);
        FacilityTemplateListRules.PageSizes.Should().Equal(10, 25, 50, 100);

        var controller = File.ReadAllText(Repo("DotNet/Link.UI/Controllers/FacilityTemplatesController.cs"));
        controller.Should().NotContain("IConfiguration").And.NotContain("GetValue").And.NotContain("appsettings");
        controller.Should().Contain("store.CountFilteredAsync").And.Contain("store.GetFilteredPageAsync");
        controller.Should().NotContain("store.CountAsync(").And.NotContain("store.GetPageAsync(");

        var view = File.ReadAllText(Repo("DotNet/Link.UI/Views/FacilityTemplates/Index.cshtml"));
        view.Should().Contain("luHoldConfirm.ask");
        view.Should().Contain("_BottomPager");
        view.Should().Contain("facilityTemplatesBulkDeleteResult");
        view.Should().Contain("au-row-hidden");
        view.Should().Contain("DeleteBulk");
        view.Should().Contain("!t.IsSystem && !t.IsDefault");
        view.Should().Contain("[\"search\"]").And.Contain("[\"vendor\"]").And.Contain("[\"kind\"]");
        view.Should().Contain("[\"sortBy\"]").And.Contain("[\"sortDir\"]");
        view.Should().Contain("auRefreshPage");
        view.Should().Contain("name=\"search\"").And.Contain("name=\"vendor\"").And.Contain("name=\"kind\"");
        view.Should().NotContain("data-sort=");
        view.Should().NotContain("au-data-table.js");
        view.Should().NotContain("confirm(");
        view.Should().NotContain("btn-outline-");

        typeof(FacilityTemplatesController).GetMethod(nameof(FacilityTemplatesController.DeleteBulk))!
            .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().Should().NotBeNull();
        typeof(FacilityTemplatesController).GetMethod(nameof(FacilityTemplatesController.DeleteInline))!
            .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().Should().NotBeNull();
    }

    private static FacilityTemplate Template(string name, bool system = false, bool isDefault = false, string? vendor = null) =>
        new() { Id = Guid.NewGuid(), Name = name, IsSystem = system, IsDefault = isDefault, VendorName = vendor };

    private static List<Guid> DeletedIds(object value) =>
        ((IEnumerable<Guid>)value.GetType().GetProperty("deletedIds")!.GetValue(value)!).ToList();

    private static List<(Guid Id, string Reason)> Failures(object value)
    {
        var failures = (System.Collections.IEnumerable)value.GetType().GetProperty("failures")!.GetValue(value)!;
        var list = new List<(Guid, string)>();
        foreach (var item in failures)
        {
            var type = item.GetType();
            list.Add(((Guid)type.GetProperty("id")!.GetValue(item)!, (string)type.GetProperty("reason")!.GetValue(item)!));
        }

        return list;
    }

    private static FacilityTemplatesController Ready(FakeTemplates templates, FakeScenarios scenarios)
    {
        var (sut, context) = Build(templates, scenarios, ready: true);
        context.Result.Should().BeNull();
        return sut;
    }

    private static (FacilityTemplatesController Sut, ActionExecutingContext Context) Build(
        FakeTemplates templates,
        FakeScenarios scenarios,
        bool ready)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFacilityTemplateStore>(templates);
        services.AddSingleton<IQueryPlanTemplateStore>(new UnusedQueryPlans());
        services.AddSingleton<INormalizationStore>(new UnusedNormalizations());
        services.AddSingleton<IOrganizationResourceMapTemplateStore>(new UnusedMaps());
        services.AddSingleton<IPatientConfigurationStore>(new UnusedPatients());
        services.AddSingleton<IScenarioStore>(scenarios);
        services.AddSingleton(new LinkAutomationEngineStatus { Ready = ready, Message = ready ? null : "Engine off." });
        var provider = services.BuildServiceProvider();
        var sut = new FacilityTemplatesController(provider.GetRequiredService<LinkAutomationEngineStatus>(), provider);
        var http = new DefaultHttpContext();
        sut.ControllerContext = new ControllerContext { HttpContext = http };
        var context = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            sut);
        sut.OnActionExecuting(context);
        return (sut, context);
    }

    private static string Repo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class FakeTemplates : IFacilityTemplateStore
    {
        public List<FacilityTemplate> Items { get; } = [];
        public List<(int Skip, int Take)> Pages { get; } = [];
        public List<Guid> Deleted { get; } = [];
        public string? LastSearch { get; private set; }
        public string? LastVendor { get; private set; }
        public string? LastKind { get; private set; }
        public string? LastSortBy { get; private set; }
        public string? LastSortDir { get; private set; }
        public Exception? DeleteError { get; set; }
        public Guid DeleteErrorId { get; set; }
        public CancellationTokenSource? CancelDuringDelete { get; set; }

        public Task<List<FacilityTemplate>> GetAllAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Items.ToList());
        }

        public Task<long> CountFilteredAsync(string? search, string? vendor, string? kind, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Remember(search, vendor, kind);
            return Task.FromResult((long)Filtered(search, vendor, kind).Count);
        }

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
            ct.ThrowIfCancellationRequested();
            Remember(search, vendor, kind, sortBy, sortDir);
            Pages.Add((skip, take));
            var ordered = Order(Filtered(search, vendor, kind), sortBy, sortDir);
            return Task.FromResult(ordered.Skip(skip).Take(take).ToList());
        }

        private void Remember(string? search, string? vendor, string? kind, string? sortBy = null, string? sortDir = null)
        {
            LastSearch = search;
            LastVendor = vendor;
            LastKind = kind;
            if (sortBy is not null)
                LastSortBy = sortBy;
            if (sortDir is not null)
                LastSortDir = sortDir;
        }

        private List<FacilityTemplate> Filtered(string? search, string? vendor, string? kind)
        {
            IEnumerable<FacilityTemplate> rows = Items;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                rows = rows.Where(item => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(vendor))
            {
                var term = vendor.Trim();
                rows = rows.Where(item => string.Equals(item.VendorName, term, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(kind, "System", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(item => item.IsSystem);
            else if (string.Equals(kind, "Custom", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(item => !item.IsSystem);

            return rows.ToList();
        }

        private static List<FacilityTemplate> Order(List<FacilityTemplate> rows, string sortBy, string sortDir)
        {
            var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            IOrderedEnumerable<FacilityTemplate> ordered = sortBy.ToLowerInvariant() switch
            {
                "vendor" => descending
                    ? rows.OrderByDescending(item => item.VendorName, StringComparer.Ordinal)
                    : rows.OrderBy(item => item.VendorName, StringComparer.Ordinal),
                "kind" => descending
                    ? rows.OrderByDescending(item => item.IsSystem)
                    : rows.OrderBy(item => item.IsSystem),
                _ => descending
                    ? rows.OrderByDescending(item => item.Name, StringComparer.Ordinal)
                    : rows.OrderBy(item => item.Name, StringComparer.Ordinal)
            };
            return ordered.ThenBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Id).ToList();
        }

        public Task<FacilityTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Items.FirstOrDefault(t => t.Id == id));
        }

        public Task<FacilityTemplate?> GetDefaultAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task UpsertAsync(FacilityTemplate template, CancellationToken ct = default) => throw new NotSupportedException();

        public Task SetDefaultAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (CancelDuringDelete is not null)
            {
                CancelDuringDelete.Cancel();
                throw new OperationCanceledException(ct);
            }

            if (DeleteError is not null && id == DeleteErrorId)
                throw DeleteError;

            ct.ThrowIfCancellationRequested();
            Deleted.Add(id);
            Items.RemoveAll(t => t.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeScenarios : IScenarioStore
    {
        public List<TestScenarioDefinition> Items { get; } = [];
        public int Reads { get; private set; }

        public Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default)
        {
            Reads++;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Items.ToList());
        }

        public Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedQueryPlans : IQueryPlanTemplateStore
    {
        public Task<List<QueryPlanTemplate>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<QueryPlanTemplate>());
        public Task<QueryPlanTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QueryPlanTemplate?> GetDefaultAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertAsync(QueryPlanTemplate template, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetDefaultAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedNormalizations : INormalizationStore
    {
        public Task<List<NormalizationOperationDefinition>> GetAllOperationsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NormalizationOperationDefinition?> GetOperationByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertOperationAsync(NormalizationOperationDefinition op, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteOperationAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<NormalizationSequenceDefinition>> GetAllSequencesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NormalizationSequenceDefinition?> GetSequenceByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertSequenceAsync(NormalizationSequenceDefinition seq, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteSequenceAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<NormalizationSuiteDefinition>> GetAllSuitesAsync(CancellationToken ct = default) => Task.FromResult(new List<NormalizationSuiteDefinition>());
        public Task<NormalizationSuiteDefinition?> GetSuiteByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NormalizationSuiteDefinition?> GetDefaultSuiteAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertSuiteAsync(NormalizationSuiteDefinition suite, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetDefaultSuiteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteSuiteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedMaps : IOrganizationResourceMapTemplateStore
    {
        public Task<List<OrganizationResourceMapTemplate>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<OrganizationResourceMapTemplate>());
        public Task<OrganizationResourceMapTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrganizationResourceMapTemplate?> GetDefaultAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertAsync(OrganizationResourceMapTemplate template, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetDefaultAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedPatients : IPatientConfigurationStore
    {
        public Task<List<PatientConfiguration>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<PatientConfiguration>());
        public Task<PatientConfiguration?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertAsync(PatientConfiguration configuration, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
