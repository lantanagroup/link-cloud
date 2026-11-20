using System.Text;
using System.Text.Json;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

public sealed class RubricCommand
{
    public ConfigurationAction Action { get; init; } = ConfigurationAction.Ok("");
    public string Redirect { get; init; } = "version";
    public string RubricId { get; init; } = "";
    public string? Semver { get; init; }
}

public sealed class RubricEditorResult
{
    public RubricEditorPage? Page { get; init; }
    public string? Redirect { get; init; }
    public ConfigurationAction? Action { get; init; }
}

public sealed class RubricImportResult
{
    public RubricImportPage Page { get; init; } = new();
    public RubricEditorPage? Editor { get; init; }
    public string? Redirect { get; init; }
    public ConfigurationAction? Action { get; init; }
}

public sealed class RubricFile
{
    public byte[]? Bytes { get; init; }
    public string ContentType { get; init; } = "application/json";
    public string FileName { get; init; } = "rubric.json";
    public string? Error { get; init; }
}

public sealed class RubricService
{
    public const int MaxUploadBytes = ConfigurationRules.MaxUploadBytes;

    private readonly IRubricRegistry? _registry;
    private readonly ILogger<RubricService> _logger;
    private RubricSettings? _settings;
    private bool _settingsLoaded;

    public RubricService(IRubricRegistry? registry, ILogger<RubricService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public static RubricService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        IRubricRegistry? client = string.IsNullOrEmpty(registry.ValidationServiceUrl)
            ? null
            : services.GetRequiredService<IRubricRegistry>();
        return new RubricService(client, services.GetRequiredService<ILogger<RubricService>>());
    }

    public bool Configured => _registry is not null;

    public async Task<RubricListPage> LoadListAsync(string? search, string? status, string? owner, string? sort, string? direction, int page, int pageSize, CancellationToken cancellationToken)
    {
        var result = new RubricListPage { Configured = Configured, Search = Clean(search), Status = StatusOf(status), Owner = Clean(owner), SortBy = SortOf(sort), SortDirection = DirectionOf(direction) };
        if (!Configured)
        {
            result = CopyList(result, ConfigurationService.ValidationNotConfigured);
            return result;
        }

        var size = pageSize is 10 or 50 ? pageSize : 20;
        var number = page < 1 ? 1 : page;
        try
        {
            var settingsTask = SettingsAsync(cancellationToken);
            var ownersTask = GetAsync<List<string>>("validation/rubrics/$owners", cancellationToken);
            var listTask = GetAsync<RubricPage<RubricSummary>>(ListPath(number - 1, size, result), cancellationToken);
            await Task.WhenAll(settingsTask, ownersTask, listTask);
            var list = await listTask;
            if (!list.Ok)
                return CopyList(result, Message(list), settings: await settingsTask, owners: (await ownersTask).Data ?? []);
            var data = list.Data ?? new RubricPage<RubricSummary>();
            return new RubricListPage
            {
                Configured = true,
                Rows = data.Content,
                Owners = (await ownersTask).Ok ? (await ownersTask).Data ?? [] : [],
                Settings = await settingsTask,
                Search = result.Search,
                Status = result.Status,
                Owner = result.Owner,
                SortBy = result.SortBy,
                SortDirection = result.SortDirection,
                Pager = new BottomPager
                {
                    Label = "Rubric pages",
                    Page = number,
                    Pages = Math.Max(data.TotalPages, 1),
                    Total = data.TotalElements,
                    PageSize = size,
                    Sizes = [10, 20, 50],
                    Path = "/Configuration/Rubrics",
                    Route = (next, nextSize) => Query(next, nextSize, result)
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric list failed");
            return CopyList(result, "Validation service call failed.");
        }
    }

    public async Task<RubricDetailPage> LoadDetailAsync(string rubricId, CancellationToken cancellationToken)
    {
        if (!IdOk(rubricId))
            return new RubricDetailPage { Configured = Configured, NotFound = true, LoadError = "That rubric id is not a valid id." };
        if (!Configured)
            return new RubricDetailPage { Configured = false, LoadError = ConfigurationService.ValidationNotConfigured };
        try
        {
            var rubricTask = GetAsync<RubricDetail>(RubricPath(rubricId), cancellationToken);
            var eventsTask = GetAsync<List<RubricLifecycleEvent>>(RubricPath(rubricId) + "/events", cancellationToken);
            var settingsTask = SettingsAsync(cancellationToken);
            await Task.WhenAll(rubricTask, eventsTask, settingsTask);
            var rubric = await rubricTask;
            if (rubric.NotFound)
                return new RubricDetailPage { Configured = true, NotFound = true, LoadError = "That rubric was not found." };
            if (!rubric.Ok || rubric.Data is null)
                return new RubricDetailPage { Configured = true, LoadError = Message(rubric) };
            var events = await eventsTask;
            return new RubricDetailPage
            {
                Configured = true,
                Rubric = rubric.Data,
                Events = events.Ok ? events.Data ?? [] : [],
                EventsError = events.Ok ? null : Message(events),
                Settings = await settingsTask
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric detail failed for {RubricId}", rubricId);
            return new RubricDetailPage { Configured = true, LoadError = "Validation service call failed." };
        }
    }

    public async Task<RubricVersionPage> LoadVersionAsync(string rubricId, string semver, string? focus, CancellationToken cancellationToken)
    {
        if (!VersionOk(rubricId, semver))
            return new RubricVersionPage { Configured = Configured, NotFound = true, RubricId = rubricId, LoadError = "That version was not found." };
        if (!Configured)
            return new RubricVersionPage { Configured = false, RubricId = rubricId, LoadError = ConfigurationService.ValidationNotConfigured };
        try
        {
            var versionTask = GetAsync<RubricVersionDetail>(VersionPath(rubricId, semver), cancellationToken);
            var rubricTask = GetAsync<RubricDetail>(RubricPath(rubricId), cancellationToken);
            var settingsTask = SettingsAsync(cancellationToken);
            await Task.WhenAll(versionTask, rubricTask, settingsTask);
            var version = await versionTask;
            if (version.NotFound)
                return new RubricVersionPage { Configured = true, NotFound = true, RubricId = rubricId, LoadError = "That version was not found." };
            if (!version.Ok || version.Data is null)
                return new RubricVersionPage { Configured = true, RubricId = rubricId, LoadError = Message(version) };
            return new RubricVersionPage
            {
                Configured = true,
                RubricId = rubricId,
                Version = version.Data,
                Rubric = (await rubricTask).Data,
                Settings = await settingsTask,
                FocusAction = focus is "publish" or "retire" or "delete" ? focus : null
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric version failed for {RubricId} {Semver}", rubricId, semver);
            return new RubricVersionPage { Configured = true, RubricId = rubricId, LoadError = "Validation service call failed." };
        }
    }

    public async Task<RubricEditorPage> LoadNewAsync(CancellationToken cancellationToken)
    {
        var settings = Configured ? await SettingsAsync(cancellationToken) : null;
        var definition = RubricDraft.NewDefinition();
        return EditorPage(definition, RubricDraft.NewDefinition(), "new", null, null, [], settings, Configured ? null : ConfigurationService.ValidationNotConfigured, Configured);
    }

    public async Task<RubricEditorPage> LoadNewVersionAsync(string rubricId, string? from, CancellationToken cancellationToken)
    {
        if (!IdOk(rubricId))
            return new RubricEditorPage { Configured = Configured, NotFound = true, LoadError = "That rubric id is not a valid id." };
        if (!Configured)
            return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "new-version", null, null, [], null, ConfigurationService.ValidationNotConfigured, false, rubricId);
        try
        {
            var rubric = await GetAsync<RubricDetail>(RubricPath(rubricId), cancellationToken);
            if (rubric.NotFound)
                return new RubricEditorPage { Configured = true, NotFound = true, LoadError = "There is no rubric with the id \"" + rubricId + "\"." };
            if (!rubric.Ok || rubric.Data is null)
                return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "new-version", null, null, [], null, Message(rubric), true, rubricId);
            var versions = (rubric.Data.Versions ?? []).Select(version => version.Semver).ToList();
            var source = !string.IsNullOrEmpty(from) && versions.Contains(from) ? from : rubric.Data.LatestPublishedSemver ?? versions.FirstOrDefault();
            if (string.IsNullOrEmpty(source))
                return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "new-version", null, null, versions, await SettingsAsync(cancellationToken), "The rubric \"" + rubricId + "\" has no version to start from.", true, rubricId);
            var version = await GetAsync<RubricVersionDetail>(VersionPath(rubricId, source), cancellationToken);
            if (!version.Ok || version.Data?.Definition is null)
                return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "new-version", null, null, versions, await SettingsAsync(cancellationToken), version.Ok ? "The definition of the version to copy could not be read." : Message(version), true, rubricId);
            var definition = RubricDraft.Normalize(version.Data.Definition);
            definition.Id = rubricId;
            definition.Semver = RubricDraft.NextVersion(versions);
            return EditorPage(definition, RubricDraft.NewDefinition(), "new-version", null, null, versions, await SettingsAsync(cancellationToken), null, true, rubricId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "New rubric version failed for {RubricId}", rubricId);
            return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "new-version", null, null, [], null, "Validation service call failed.", true, rubricId);
        }
    }

    public async Task<RubricEditorPage> LoadEditAsync(string rubricId, string semver, CancellationToken cancellationToken)
    {
        if (!VersionOk(rubricId, semver))
            return new RubricEditorPage { Configured = Configured, NotFound = true, LoadError = "That version was not found." };
        if (!Configured)
            return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "edit", null, null, [], null, ConfigurationService.ValidationNotConfigured, false, rubricId, semver);
        try
        {
            var versionTask = GetAsync<RubricVersionDetail>(VersionPath(rubricId, semver), cancellationToken);
            var rubricTask = GetAsync<RubricDetail>(RubricPath(rubricId), cancellationToken);
            var settingsTask = SettingsAsync(cancellationToken);
            await Task.WhenAll(versionTask, rubricTask, settingsTask);
            var version = await versionTask;
            if (version.NotFound)
                return new RubricEditorPage { Configured = true, NotFound = true, LoadError = "The rubric \"" + rubricId + "\" has no version " + semver + "." };
            if (!version.Ok || version.Data is null)
                return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "edit", null, null, [], await settingsTask, Message(version), true, rubricId, semver);
            if (version.Data.Definition is null)
                return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "edit", null, null, [], await settingsTask, "This version's definition could not be read.", true, rubricId, semver);
            var definition = RubricDraft.Normalize(version.Data.Definition);
            var rubric = (await rubricTask).Data;
            var existing = (rubric?.Versions ?? []).Select(item => item.Semver).Where(item => item != semver).ToList();
            var locked = version.Data.Status == "DRAFT"
                ? null
                : "Version " + semver + " is " + version.Data.Status.ToLowerInvariant() + " and cannot be changed. To change the rubric, create a new version from it.";
            return EditorPage(definition, RubricDraft.Clone(definition), "edit", version.Data.Checksum, semver, existing, await settingsTask, null, true, rubricId, semver, locked);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric editor failed for {RubricId} {Semver}", rubricId, semver);
            return EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), "edit", null, null, [], null, "Validation service call failed.", true, rubricId, semver);
        }
    }

    public async Task<RubricEditorResult> PostEditorAsync(RubricEditorPost post, CancellationToken cancellationToken)
    {
        if (!Configured)
            return Page(EditorPage(RubricDraft.NewDefinition(), RubricDraft.NewDefinition(), post.Mode, null, null, [], null, ConfigurationService.ValidationNotConfigured, false, post.RouteRubricId, post.RouteSemver));
        var settings = await SettingsAsync(cancellationToken);
        if (post.Intent == "reload")
        {
            if (post.Mode == "edit" && !string.IsNullOrEmpty(post.SavedSemver))
                return Page(await LoadEditAsync(post.RouteRubricId, post.SavedSemver, cancellationToken));
            if (post.Mode == "new-version")
                return Page(await LoadNewVersionAsync(post.RouteRubricId, null, cancellationToken));
            return Page(await LoadNewAsync(cancellationToken));
        }

        var state = RubricEditorLogic.Apply(post, settings);
        if (post.Intent is "save" or "save-anyway" or "validate")
            return await SaveOrValidateAsync(post, state, settings, cancellationToken);
        return Page(EditorFrom(state, settings, null, false, [], null, state.Failure, false));
    }

    public RubricImportPage LoadImport() => new() { Configured = Configured, LoadError = Configured ? null : ConfigurationService.ValidationNotConfigured };

    public async Task<RubricImportResult> PostImportAsync(string? text, string? fileName, byte[]? file, string intent, CancellationToken cancellationToken)
    {
        var page = LoadImport();
        if (!Configured)
            return new RubricImportResult { Page = page };
        var body = file is { Length: > 0 } ? Encoding.UTF8.GetString(file) : text ?? "";
        if (file is { Length: > MaxUploadBytes } || body.Length > MaxUploadBytes)
            return new RubricImportResult { Page = page with { Text = "", FileError = "That file is larger than this page accepts." } };
        if (!string.IsNullOrEmpty(fileName) && !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            return new RubricImportResult { Page = page with { FileError = "\"" + fileName + "\" is not a .yaml, .yml or .json file." } };
        page = page with { Text = body, FileName = string.IsNullOrEmpty(fileName) ? null : fileName };
        if (string.IsNullOrWhiteSpace(body))
            return new RubricImportResult { Page = page with { Failure = "Paste a definition, or choose a file, before reviewing it." } };
        try
        {
            if (intent == "review-next")
                return await ReviewNextAsync(page, cancellationToken);
            var review = await ValidateTextAsync(body, Format(body, fileName), cancellationToken);
            if (review.Failure is not null)
                return new RubricImportResult { Page = page with { Failure = review.Failure } };
            var effect = RubricCompare.ImportEffect(review.Result);
            if (intent == "open")
                return await OpenReviewedAsync(review.Result, cancellationToken);
            if (intent == "import")
                return await ImportReviewedAsync(page, review.Result, effect, cancellationToken);
            return new RubricImportResult { Page = page with { Review = review.Result, Effect = effect } };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric import failed");
            return new RubricImportResult { Page = page with { Failure = "Validation service call failed." } };
        }
    }

    public async Task<RubricComparePage> LoadCompareAsync(string rubricId, string? from, string? to, CancellationToken cancellationToken)
    {
        if (!IdOk(rubricId))
            return new RubricComparePage { Configured = Configured, NotFound = true, RubricId = rubricId, LoadError = "That rubric id is not a valid id." };
        if (!Configured)
            return new RubricComparePage { Configured = false, RubricId = rubricId, LoadError = ConfigurationService.ValidationNotConfigured };
        try
        {
            var rubric = await GetAsync<RubricDetail>(RubricPath(rubricId), cancellationToken);
            if (rubric.NotFound)
                return new RubricComparePage { Configured = true, NotFound = true, RubricId = rubricId, LoadError = "That rubric was not found." };
            if (!rubric.Ok || rubric.Data is null)
                return new RubricComparePage { Configured = true, RubricId = rubricId, LoadError = Message(rubric) };
            var chosen = RubricCompare.DefaultComparison(rubric.Data);
            var left = string.IsNullOrEmpty(from) ? chosen?.From ?? "" : from;
            var right = string.IsNullOrEmpty(to) ? chosen?.To ?? "" : to;
            if (!RubricDraft.SemverPattern.IsMatch(left) || !RubricDraft.SemverPattern.IsMatch(right))
                return new RubricComparePage { Configured = true, RubricId = rubricId, Rubric = rubric.Data, LoadError = "This rubric needs two versions to compare." };
            var beforeTask = GetAsync<RubricVersionDetail>(VersionPath(rubricId, left), cancellationToken);
            var afterTask = GetAsync<RubricVersionDetail>(VersionPath(rubricId, right), cancellationToken);
            await Task.WhenAll(beforeTask, afterTask);
            var before = await beforeTask;
            var after = await afterTask;
            if (!before.Ok || before.Data?.Definition is null || !after.Ok || after.Data?.Definition is null)
                return new RubricComparePage { Configured = true, RubricId = rubricId, Rubric = rubric.Data, From = left, To = right, LoadError = "One of those versions could not be read." };
            return new RubricComparePage
            {
                Configured = true,
                RubricId = rubricId,
                Rubric = rubric.Data,
                From = left,
                To = right,
                Before = before.Data,
                After = after.Data,
                Comparison = RubricCompare.CompareDefinitions(RubricDraft.Normalize(before.Data.Definition), RubricDraft.Normalize(after.Data.Definition))
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric compare failed for {RubricId}", rubricId);
            return new RubricComparePage { Configured = true, RubricId = rubricId, LoadError = "Validation service call failed." };
        }
    }

    public async Task<RubricDryRunPage> LoadDryRunAsync(string rubricId, string semver, CancellationToken cancellationToken)
    {
        var version = await LoadVersionAsync(rubricId, semver, null, cancellationToken);
        return new RubricDryRunPage
        {
            Configured = version.Configured,
            LoadError = version.LoadError,
            NotFound = version.NotFound,
            RubricId = rubricId,
            Semver = semver,
            Version = version.Version
        };
    }

    public async Task<RubricDryRunPage> PostDryRunAsync(string rubricId, string semver, string? text, byte[]? file, CancellationToken cancellationToken)
    {
        var page = await LoadDryRunAsync(rubricId, semver, cancellationToken);
        if (page.LoadError is not null || page.Version is null)
            return page;
        var sampleText = file is { Length: > 0 } ? Encoding.UTF8.GetString(file) : text ?? "";
        if (sampleText.Length > MaxUploadBytes)
            return page with { SampleError = "That sample is larger than this page accepts." };
        var reading = RubricSample.Read(sampleText);
        if (reading.Error is not null)
            return page with { SampleError = reading.Error, SampleText = file is { Length: > 0 } ? "" : sampleText };
        try
        {
            var payload = JsonSerializer.Serialize(new { payload = reading.Sample }, RubricJson.Options);
            var call = await SendAsync<RubricRunResult>(HttpMethod.Post, "validation/v2/rubrics/" + Uri.EscapeDataString(rubricId) + "/versions/" + Uri.EscapeDataString(semver) + "/$dry-run", payload, "application/json", null, cancellationToken);
            if (!call.Ok || call.Data is null)
                return page with { Failure = Message(call), SampleDescription = reading.Description, SampleText = file is { Length: > 0 } ? "" : sampleText };
            return page with { Result = call.Data, SampleDescription = reading.Description, SampleText = file is { Length: > 0 } ? "" : sampleText };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric dry run failed for {RubricId} {Semver}", rubricId, semver);
            return page with { Failure = "Validation service call failed." };
        }
    }

    public async Task<RubricCommand> PublishAsync(string rubricId, string semver, CancellationToken cancellationToken) =>
        await LifecycleAsync(rubricId, semver, "publish", "$publish", cancellationToken);

    public async Task<RubricCommand> RetireAsync(string rubricId, string semver, CancellationToken cancellationToken) =>
        await LifecycleAsync(rubricId, semver, "retire", "$retire", cancellationToken);

    public async Task<RubricCommand> DeleteAsync(string rubricId, string semver, string? checksum, CancellationToken cancellationToken)
    {
        var page = await LoadVersionAsync(rubricId, semver, null, cancellationToken);
        if (page.Version is null)
            return new RubricCommand { Action = ConfigurationAction.Fail(page.LoadError ?? "That version was not found."), Redirect = "list", RubricId = rubricId };
        var enabled = page.Version.Definition?.Checks.Count(RubricText.IsCheckEnabled);
        var plan = RubricLifecycle.Plan("delete", rubricId, semver, page.Version.Status, page.Version.DryRunCompletedAt, page.Rubric, page.Settings, enabled);
        if (plan.Blocked is not null)
            return new RubricCommand { Action = ConfigurationAction.Fail(plan.Blocked), Redirect = "version", RubricId = rubricId, Semver = semver };
        if (!Configured)
            return new RubricCommand { Action = ConfigurationAction.Fail(ConfigurationService.ValidationNotConfigured), Redirect = "list" };
        try
        {
            var headers = string.IsNullOrWhiteSpace(checksum) ? null : new Dictionary<string, string> { ["If-Match"] = Quote(checksum) };
            var call = await SendAsync<JsonElement>(HttpMethod.Delete, VersionPath(rubricId, semver), null, null, headers, cancellationToken);
            if (call.Precondition)
                return new RubricCommand { Action = ConfigurationAction.Fail("This draft changed after you opened it. Review it again before deleting it."), Redirect = "version", RubricId = rubricId, Semver = semver };
            if (!call.Ok)
                return new RubricCommand { Action = ConfigurationAction.Fail(Message(call)), Redirect = "version", RubricId = rubricId, Semver = semver };
            var removed = RubricLifecycle.DeletingRemovesRubric(page.Rubric);
            return new RubricCommand
            {
                Action = ConfigurationAction.Ok(removed ? "The draft was deleted, and it was the only version, so the rubric was removed." : "Draft " + semver + " was deleted."),
                Redirect = removed ? "list" : "detail",
                RubricId = rubricId,
                Semver = semver
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric delete failed for {RubricId} {Semver}", rubricId, semver);
            return new RubricCommand { Action = ConfigurationAction.Fail("Validation service call failed."), Redirect = "version", RubricId = rubricId, Semver = semver };
        }
    }

    public async Task<RubricFile> ExportAsync(string rubricId, string semver, string? format, CancellationToken cancellationToken)
    {
        var yaml = string.Equals(format, "yaml", StringComparison.OrdinalIgnoreCase);
        if (!VersionOk(rubricId, semver) || !Configured)
            return new RubricFile { Error = "That version could not be exported." };
        var headers = new Dictionary<string, string> { ["Accept"] = yaml ? "application/yaml" : "application/json" };
        var response = await _registry!.SendAsync(HttpMethod.Get, VersionPath(rubricId, semver) + "/definition", null, null, headers, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var call = RubricResponses.Read<JsonElement>(response);
            return new RubricFile { Error = Message(call) };
        }

        var text = response.Body ?? "";
        var extension = yaml ? "yaml" : "json";
        return new RubricFile
        {
            Bytes = Encoding.UTF8.GetBytes(text),
            ContentType = yaml ? "application/yaml" : "application/json",
            FileName = rubricId + "-" + semver + "." + extension
        };
    }

    private async Task<RubricCommand> LifecycleAsync(string rubricId, string semver, string action, string operation, CancellationToken cancellationToken)
    {
        var page = await LoadVersionAsync(rubricId, semver, null, cancellationToken);
        if (page.Version is null)
            return new RubricCommand { Action = ConfigurationAction.Fail(page.LoadError ?? "That version was not found."), Redirect = "list", RubricId = rubricId };
        var enabled = page.Version.Definition is null ? (int?)null : page.Version.Definition.Checks.Count(RubricText.IsCheckEnabled);
        var plan = RubricLifecycle.Plan(action, rubricId, semver, page.Version.Status, page.Version.DryRunCompletedAt, page.Rubric, page.Settings, enabled);
        if (plan.Blocked is not null)
            return new RubricCommand { Action = ConfigurationAction.Fail(plan.Blocked), Redirect = "version", RubricId = rubricId, Semver = semver };
        try
        {
            var call = await SendAsync<RubricVersionSummary>(HttpMethod.Post, VersionPath(rubricId, semver) + "/" + operation, null, null, null, cancellationToken);
            if (!call.Ok)
                return new RubricCommand { Action = ConfigurationAction.Fail(Message(call)), Redirect = "version", RubricId = rubricId, Semver = semver };
            var verb = action == "publish" ? "published" : "retired";
            return new RubricCommand { Action = ConfigurationAction.Ok("Version " + semver + " was " + verb + "."), Redirect = "version", RubricId = rubricId, Semver = semver };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric {Action} failed for {RubricId} {Semver}", action, rubricId, semver);
            return new RubricCommand { Action = ConfigurationAction.Fail("Validation service call failed."), Redirect = "version", RubricId = rubricId, Semver = semver };
        }
    }

    private async Task<RubricEditorResult> SaveOrValidateAsync(RubricEditorPost post, RubricEditorState state, RubricSettings? settings, CancellationToken cancellationToken)
    {
        var issues = RubricEditorLogic.Issues(state.Definition, settings, state.ExistingVersions, state.SavedSemver);
        var validate = post.Intent == "validate";
        var overwrite = post.Intent == "save-anyway";
        if (!validate && issues.Count > 0)
            return Page(EditorFrom(state, settings, null, true, issues, null, state.Failure, false));
        var cleaned = RubricDraft.Clean(state.Definition, settings);
        var json = RubricJson.Write(cleaned);
        if (json.Length > RubricDraft.PayloadCap(settings))
            return Page(EditorFrom(state, settings, null, true, issues, null, "That definition is larger than this environment accepts.", false));
        try
        {
            if (validate)
            {
                var review = await ValidateTextAsync(json, "json", cancellationToken);
                if (review.Failure is not null)
                    return Page(EditorFrom(state, settings, null, true, issues, null, review.Failure, false));
                var server = (review.Result?.Errors ?? []).Select(error => RubricDraft.PlaceServerError(error, state.Definition)).ToList();
                if (post.Mode == "new" && review.Result?.RubricExists == true)
                {
                    RubricDraft.IdTakenMessage(cleaned.Id, out var taken);
                    server.Add(taken);
                }

                issues.AddRange(server);
                var notice = issues.Count == 0 ? "No problems found. The draft is ready to save." : null;
                return Page(EditorFrom(state, settings, null, true, issues, notice, null, false));
            }

            if (post.Mode == "new")
            {
                var existing = await GetAsync<RubricDetail>(RubricPath(cleaned.Id), cancellationToken);
                if (existing.Ok)
                {
                    RubricDraft.IdTakenMessage(cleaned.Id, out var taken);
                    return Page(EditorFrom(state, settings, null, true, [.. issues, taken], null, null, false));
                }

                if (!existing.NotFound)
                    return Page(EditorFrom(state, settings, null, true, issues, null, Message(existing), false));
            }

            var plan = RubricEditorLogic.PlanSave(state.SavedSemver, overwrite ? null : state.SavedChecksum, cleaned.Semver, overwrite);
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(plan.ExpectedChecksum))
                headers["If-Match"] = Quote(plan.ExpectedChecksum);
            if (plan.MustNotExist)
                headers["If-None-Match"] = "*";
            var saved = await SendAsync<RubricVersionSummary>(HttpMethod.Post, RubricPath(cleaned.Id) + "/versions", json, "application/json", headers, cancellationToken);
            if (saved.Precondition && !string.IsNullOrEmpty(plan.ExpectedChecksum))
                return Page(EditorFrom(state, settings, null, true, issues, null, null, true));
            if (saved.Precondition)
                return Page(EditorFrom(state, settings, null, true, [.. issues, new RubricIssue("semver", "Version " + cleaned.Semver + " of this rubric already exists. Choose another version number.", "semver")], null, null, false));
            if (saved.Conflict)
                return Page(EditorFrom(state, settings, null, true, [.. issues, new RubricIssue("semver", "Version " + cleaned.Semver + " is published or retired and cannot be replaced. Choose another version number.", "semver")], null, null, false));
            if (!saved.Ok || saved.Data is null)
            {
                var server = (saved.Errors ?? []).Select(error => RubricDraft.PlaceServerError(error, state.Definition)).ToList();
                if (server.Count == 0)
                    return Page(EditorFrom(state, settings, null, true, issues, null, Message(saved), false));
                return Page(EditorFrom(state, settings, null, true, server, null, null, false));
            }

            var message = "Draft saved.";
            if (plan.Moving && !string.IsNullOrEmpty(plan.PreviousSemver))
            {
                var deleted = await SendAsync<JsonElement>(HttpMethod.Delete, VersionPath(cleaned.Id, plan.PreviousSemver), null, null, string.IsNullOrEmpty(plan.PreviousChecksum) ? null : new Dictionary<string, string> { ["If-Match"] = Quote(plan.PreviousChecksum) }, cancellationToken);
                message = deleted.Ok || deleted.NotFound
                    ? "Draft saved as version " + saved.Data.Semver + "; the draft under " + plan.PreviousSemver + " was deleted."
                    : deleted.Precondition
                        ? "Draft saved as version " + saved.Data.Semver + ". The draft under " + plan.PreviousSemver + " was kept: someone saved changes to it after you opened it. Review it on the rubric's page, and delete it there if it is not needed."
                        : "Draft saved as version " + saved.Data.Semver + ". The draft under " + plan.PreviousSemver + " could not be deleted; delete it from the rubric's page.";
            }

            return new RubricEditorResult
            {
                Redirect = "/Configuration/Rubrics/" + Uri.EscapeDataString(cleaned.Id) + "/Versions/" + Uri.EscapeDataString(saved.Data.Semver) + "/Edit",
                Action = ConfigurationAction.Ok(message)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rubric draft save failed");
            return Page(EditorFrom(state, settings, null, true, issues, null, "Validation service call failed.", false));
        }
    }

    private async Task<RubricImportResult> ReviewNextAsync(RubricImportPage page, CancellationToken cancellationToken)
    {
        var current = await ValidateTextAsync(page.Text, Format(page.Text, page.FileName), cancellationToken);
        if (current.Result?.Definition is null)
            return new RubricImportResult { Page = page with { Failure = current.Failure ?? "The definition could not be read." } };
        var rubric = await GetAsync<RubricDetail>(RubricPath(current.Result.Definition.Id), cancellationToken);
        if (!rubric.Ok || rubric.Data is null)
            return new RubricImportResult { Page = page with { Failure = rubric.NotFound ? "There is no rubric with that id to number a new version from." : Message(rubric) } };
        var renumbered = RubricDraft.Clone(current.Result.Definition);
        renumbered.Semver = RubricDraft.NextVersion((rubric.Data.Versions ?? []).Select(version => version.Semver));
        var json = RubricJson.Write(renumbered);
        var review = await ValidateTextAsync(json, "json", cancellationToken);
        return new RubricImportResult { Page = page with { Text = json, FileName = null, Review = review.Result, Effect = RubricCompare.ImportEffect(review.Result), Failure = review.Failure } };
    }

    private async Task<RubricImportResult> OpenReviewedAsync(RubricValidationResult? review, CancellationToken cancellationToken)
    {
        if (review?.Definition is null)
            return new RubricImportResult { Page = LoadImport() with { Failure = "Review a definition before opening it." } };
        var definition = RubricDraft.Normalize(review.Definition);
        var settings = await SettingsAsync(cancellationToken);
        if (review.RubricExists == true && IdOk(definition.Id))
        {
            var rubric = await GetAsync<RubricDetail>(RubricPath(definition.Id), cancellationToken);
            var versions = (rubric.Data?.Versions ?? []).Select(version => version.Semver).ToList();
            definition.Id = definition.Id;
            return new RubricImportResult { Editor = EditorPage(definition, RubricDraft.NewDefinition(), "new-version", null, null, versions, settings, null, true, definition.Id) };
        }

        return new RubricImportResult { Editor = EditorPage(definition, RubricDraft.NewDefinition(), "new", null, null, [], settings, null, true) };
    }

    private async Task<RubricImportResult> ImportReviewedAsync(RubricImportPage page, RubricValidationResult? review, string? effect, CancellationToken cancellationToken)
    {
        if (review?.Definition is null || effect is not ("new-rubric" or "new-version" or "replace-draft"))
            return new RubricImportResult { Page = page with { Review = review, Effect = effect, Failure = "Review the definition before importing it. An import never publishes." } };
        var headers = new Dictionary<string, string>();
        if (effect == "replace-draft")
        {
            if (string.IsNullOrEmpty(review.ExistingChecksum))
                return new RubricImportResult { Page = page with { Review = review, Effect = effect, Failure = "This draft has no checksum to match. Review it again." } };
            headers["If-Match"] = Quote(review.ExistingChecksum);
        }
        else
            headers["If-None-Match"] = "*";
        var json = RubricJson.Write(RubricDraft.Clean(RubricDraft.Normalize(review.Definition)));
        var saved = await SendAsync<RubricVersionSummary>(HttpMethod.Post, RubricPath(review.Definition.Id) + "/versions", json, "application/json", headers, cancellationToken);
        if (saved.Precondition)
            return new RubricImportResult { Page = page with { Failure = "This rubric changed while you were reviewing it. Review it again to see what importing would do now." } };
        if (!saved.Ok || saved.Data is null)
            return new RubricImportResult { Page = page with { Review = review, Effect = effect, Failure = Message(saved) } };
        return new RubricImportResult
        {
            Redirect = "/Configuration/Rubrics/" + Uri.EscapeDataString(saved.Data.RubricId) + "/Versions/" + Uri.EscapeDataString(saved.Data.Semver),
            Action = ConfigurationAction.Ok("Draft " + saved.Data.Semver + " was imported. Nothing was published.")
        };
    }

    private async Task<(RubricValidationResult? Result, string? Failure)> ValidateTextAsync(string text, string format, CancellationToken cancellationToken)
    {
        var contentType = format == "yaml" ? "application/yaml" : "application/json";
        var call = await SendAsync<RubricValidationResult>(HttpMethod.Post, "validation/rubrics/$validate", text, contentType, null, cancellationToken);
        if (!call.Ok && call.Data is null)
            return (null, Message(call));
        var result = call.Data ?? new RubricValidationResult();
        result.Errors ??= [];
        return (result, null);
    }

    private async Task<RubricSettings?> SettingsAsync(CancellationToken cancellationToken)
    {
        if (_settingsLoaded || _registry is null)
            return _settings;
        _settingsLoaded = true;
        var call = await GetAsync<RubricSettings>("validation/rubrics/$settings", cancellationToken);
        _settings = call.Ok ? call.Data : null;
        return _settings;
    }

    private Task<RubricResponse<T>> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Get, path, null, null, null, cancellationToken);

    private async Task<RubricResponse<T>> SendAsync<T>(HttpMethod method, string path, string? body, string? contentType, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        var response = await _registry!.SendAsync(method, path, body, contentType, headers, cancellationToken);
        return RubricResponses.Read<T>(response);
    }

    private static RubricEditorResult Page(RubricEditorPage page) => new() { Page = page };

    private RubricEditorPage EditorFrom(RubricEditorState state, RubricSettings? settings, string? locked, bool showIssues, IReadOnlyList<RubricIssue> issues, string? notice, string? failure, bool stale) =>
        EditorPage(state.Definition, state.Baseline, state.Mode, state.SavedChecksum, state.SavedSemver, state.ExistingVersions, settings, failure, true, state.Mode == "new" ? "" : state.Definition.Id, state.Mode == "edit" ? state.SavedSemver ?? state.Definition.Semver : "", locked, state.EditIndex, showIssues, issues, notice, stale);

    private static RubricEditorPage EditorPage(
        RubricDefinition definition,
        RubricDefinition baseline,
        string mode,
        string? checksum,
        string? savedSemver,
        IReadOnlyList<string> existing,
        RubricSettings? settings,
        string? failure,
        bool configured,
        string rubricId = "",
        string routeSemver = "",
        string? locked = null,
        int editIndex = -1,
        bool showIssues = false,
        IReadOnlyList<RubricIssue>? issues = null,
        string? notice = null,
        bool stale = false) => new()
        {
            Configured = configured,
            LoadError = configured ? null : failure,
            Mode = mode,
            Locked = locked,
            Definition = definition,
            Baseline = baseline,
            SavedChecksum = checksum,
            SavedSemver = savedSemver,
            ExistingVersions = existing,
            Settings = settings,
            Issues = issues ?? [],
            ShowIssues = showIssues,
            Notice = notice,
            Failure = configured ? failure : null,
            StaleDraft = stale,
            EditIndex = editIndex,
            RubricId = rubricId,
            RouteSemver = routeSemver
        };

    private static RubricListPage CopyList(RubricListPage page, string error, RubricSettings? settings = null, IReadOnlyList<string>? owners = null) => new()
    {
        Configured = page.Configured,
        LoadError = error,
        Search = page.Search,
        Status = page.Status,
        Owner = page.Owner,
        SortBy = page.SortBy,
        SortDirection = page.SortDirection,
        Settings = settings,
        Owners = owners ?? []
    };

    private static Dictionary<string, string> Query(int page, int size, RubricListPage pageState)
    {
        var query = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = size.ToString(),
            ["sort"] = pageState.SortBy,
            ["direction"] = pageState.SortDirection
        };
        if (pageState.Search.Length > 0)
            query["search"] = pageState.Search;
        if (pageState.Status.Length > 0)
            query["status"] = pageState.Status;
        if (pageState.Owner.Length > 0)
            query["owner"] = pageState.Owner;
        return query;
    }

    private static string ListPath(int page, int size, RubricListPage pageState)
    {
        var parts = new List<string>
        {
            "page=" + page,
            "size=" + size,
            "allVersions=true",
            "sort=" + Uri.EscapeDataString(pageState.SortBy + "," + pageState.SortDirection)
        };
        if (pageState.Search.Length > 0)
            parts.Add("search=" + Uri.EscapeDataString(pageState.Search));
        if (pageState.Status.Length > 0)
            parts.Add("status=" + pageState.Status);
        if (pageState.Owner.Length > 0)
            parts.Add("owner=" + Uri.EscapeDataString(pageState.Owner));
        return "validation/rubrics?" + string.Join("&", parts);
    }

    private static string RubricPath(string id) => "validation/rubrics/" + Uri.EscapeDataString(id);
    private static string VersionPath(string id, string semver) => RubricPath(id) + "/versions/" + Uri.EscapeDataString(semver);
    private static bool IdOk(string? id) => !string.IsNullOrEmpty(id) && RubricDraft.RubricIdPattern.IsMatch(id);
    private static bool VersionOk(string? id, string? semver) => IdOk(id) && !string.IsNullOrEmpty(semver) && RubricDraft.SemverPattern.IsMatch(semver);

    private static string Clean(string? value)
    {
        var sanitized = (value ?? "").Sanitize().Trim();
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }

    private static string StatusOf(string? status) => status is "DRAFT" or "PUBLISHED" or "RETIRED" ? status : "";
    private static string SortOf(string? sort) => sort is "rubricId" or "title" or "owner" or "updatedAt" or "createdAt" ? sort : "rubricId";
    private static string DirectionOf(string? direction) => direction == "desc" ? "desc" : "asc";
    private static string Format(string text, string? fileName)
    {
        if (!string.IsNullOrEmpty(fileName))
            return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "yaml";
        return text.TrimStart().StartsWith('{') ? "json" : "yaml";
    }

    private static string Quote(string checksum) => checksum.StartsWith('"') ? checksum : "\"" + checksum + "\"";

    private static string Message<T>(RubricResponse<T> call)
    {
        var message = string.IsNullOrWhiteSpace(call.Message)
            ? call.Status == 0 ? "Validation could not be reached." : "Validation returned HTTP " + call.Status + "."
            : call.Message.Trim();
        if (call.Errors.Count > 0)
            message += " " + string.Join(" ", call.Errors);
        if (!string.IsNullOrWhiteSpace(call.TraceId) && message.IndexOf(call.TraceId, StringComparison.Ordinal) < 0)
            message += " Trace id: " + call.TraceId;
        return message;
    }
}

public sealed class RubricResponse<T>
{
    public int Status { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public string? TraceId { get; init; }
    public bool Ok => Status is >= 200 and < 300;
    public bool NotFound => Status == 404;
    public bool Precondition => Status == 412;
    public bool Conflict => Status == 409;
}

public static class RubricResponses
{
    public static RubricResponse<T> Read<T>(LinkApiResponse<string> response)
    {
        var raw = response.Body ?? response.RawBody;
        RubricEnvelope<T>? envelope = null;
        if (!string.IsNullOrWhiteSpace(raw) && raw.TrimStart().StartsWith('{'))
        {
            try
            {
                envelope = JsonSerializer.Deserialize<RubricEnvelope<T>>(raw, RubricJson.Options);
            }
            catch (JsonException)
            {
                envelope = null;
            }
        }

        return new RubricResponse<T>
        {
            Status = response.StatusCode,
            Message = envelope?.Message,
            Data = envelope is null ? default : envelope.Data,
            Errors = envelope?.Errors ?? [],
            TraceId = response.TraceId
        };
    }
}
