using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed partial class ConfigurationController
{
    private const long RubricUploadBytes = RubricService.MaxUploadBytes;

    [HttpGet("Rubrics")]
    public async Task<IActionResult> Rubrics(string? search, string? status, string? owner, string? sort, string? direction, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        Section("rubrics", "Validation rubrics");
        return View(await _rubrics.LoadListAsync(search, status, owner, sort, direction, page, pageSize, cancellationToken));
    }

    [HttpGet("Rubrics/Import")]
    public IActionResult ImportRubric()
    {
        Section("rubrics", "Import a rubric");
        return View("RubricImport", _rubrics.LoadImport());
    }

    [HttpPost("Rubrics/Import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(RubricUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RubricUploadBytes, ValueLengthLimit = (int)RubricUploadBytes)]
    public async Task<IActionResult> PostImportRubric(string? text, string? intent, IFormFile? file, CancellationToken cancellationToken)
    {
        Section("rubrics", "Import a rubric");
        var bytes = await ReadUploadAsync(file, cancellationToken);
        if (file is { Length: > RubricUploadBytes })
            return View("RubricImport", _rubrics.LoadImport() with { FileError = "That file is larger than this page accepts." });
        var result = await _rubrics.PostImportAsync(text, file?.FileName, bytes, intent ?? "review", cancellationToken);
        if (result.Editor is not null)
            return View("RubricEditor", result.Editor);
        if (result.Redirect is not null)
        {
            Temp(result.Action ?? ConfigurationAction.Ok("Draft imported."));
            return Redirect(result.Redirect);
        }

        return View("RubricImport", result.Page);
    }

    [HttpGet("Rubrics/New")]
    public async Task<IActionResult> NewRubric(CancellationToken cancellationToken)
    {
        Section("rubrics", "New rubric");
        return View("RubricEditor", await _rubrics.LoadNewAsync(cancellationToken));
    }

    [HttpPost("Rubrics/New")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueLengthLimit = (int)RubricUploadBytes)]
    public async Task<IActionResult> PostNewRubric(CancellationToken cancellationToken) =>
        await PostedEditorAsync("new", "", "", "New rubric", cancellationToken);

    [HttpGet("Rubrics/{rubricId}")]
    public async Task<IActionResult> Rubric(string rubricId, CancellationToken cancellationToken)
    {
        Section("rubrics", "Validation rubric");
        return View(await _rubrics.LoadDetailAsync(rubricId, cancellationToken));
    }

    [HttpGet("Rubrics/{rubricId}/Compare")]
    public async Task<IActionResult> CompareRubrics(string rubricId, string? from, string? to, CancellationToken cancellationToken)
    {
        Section("rubrics", "Compare versions");
        return View("RubricCompare", await _rubrics.LoadCompareAsync(rubricId, from, to, cancellationToken));
    }

    [HttpGet("Rubrics/{rubricId}/Versions/New")]
    public async Task<IActionResult> NewRubricVersion(string rubricId, string? from, CancellationToken cancellationToken)
    {
        Section("rubrics", "New version");
        return View("RubricEditor", await _rubrics.LoadNewVersionAsync(rubricId, from, cancellationToken));
    }

    [HttpPost("Rubrics/{rubricId}/Versions/New")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueLengthLimit = (int)RubricUploadBytes)]
    public async Task<IActionResult> PostNewRubricVersion(string rubricId, CancellationToken cancellationToken) =>
        await PostedEditorAsync("new-version", rubricId, "", "New version", cancellationToken);

    [HttpGet("Rubrics/{rubricId}/Versions/{semver}")]
    public async Task<IActionResult> RubricVersion(string rubricId, string semver, [FromQuery(Name = "do")] string? doAction, CancellationToken cancellationToken)
    {
        Section("rubrics", "Rubric version");
        return View(await _rubrics.LoadVersionAsync(rubricId, semver, doAction, cancellationToken));
    }

    [HttpGet("Rubrics/{rubricId}/Versions/{semver}/Edit")]
    public async Task<IActionResult> EditRubric(string rubricId, string semver, CancellationToken cancellationToken)
    {
        Section("rubrics", "Edit rubric");
        return View("RubricEditor", await _rubrics.LoadEditAsync(rubricId, semver, cancellationToken));
    }

    [HttpPost("Rubrics/{rubricId}/Versions/{semver}/Edit")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueLengthLimit = (int)RubricUploadBytes)]
    public async Task<IActionResult> PostEditRubric(string rubricId, string semver, CancellationToken cancellationToken) =>
        await PostedEditorAsync("edit", rubricId, semver, "Edit rubric", cancellationToken);

    [HttpGet("Rubrics/{rubricId}/Versions/{semver}/DryRun")]
    public async Task<IActionResult> DryRunRubric(string rubricId, string semver, CancellationToken cancellationToken)
    {
        Section("rubrics", "Dry run");
        return View("RubricDryRun", await _rubrics.LoadDryRunAsync(rubricId, semver, cancellationToken));
    }

    [HttpPost("Rubrics/{rubricId}/Versions/{semver}/DryRun")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(RubricUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RubricUploadBytes, ValueLengthLimit = (int)RubricUploadBytes)]
    public async Task<IActionResult> PostDryRunRubric(string rubricId, string semver, string? sampleText, IFormFile? sampleFile, CancellationToken cancellationToken)
    {
        Section("rubrics", "Dry run");
        if (sampleFile is { Length: > RubricUploadBytes })
        {
            var refused = await _rubrics.LoadDryRunAsync(rubricId, semver, cancellationToken);
            return View("RubricDryRun", refused with { SampleError = "That sample is larger than this page accepts." });
        }

        var bytes = await ReadUploadAsync(sampleFile, cancellationToken);
        return View("RubricDryRun", await _rubrics.PostDryRunAsync(rubricId, semver, sampleText, bytes, cancellationToken));
    }

    [HttpGet("Rubrics/{rubricId}/Versions/{semver}/Export")]
    public async Task<IActionResult> ExportRubric(string rubricId, string semver, string? format, CancellationToken cancellationToken)
    {
        var file = await _rubrics.ExportAsync(rubricId, semver, format, cancellationToken);
        if (file.Bytes is null)
        {
            Temp(ConfigurationAction.Fail(file.Error ?? "That version could not be exported."));
            return RedirectToAction(nameof(RubricVersion), new { rubricId, semver });
        }

        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpPost("Rubrics/{rubricId}/Versions/{semver}/Publish")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PublishRubric(string rubricId, string semver, CancellationToken cancellationToken) =>
        RunCommandAsync(() => _rubrics.PublishAsync(rubricId, semver, cancellationToken));

    [HttpPost("Rubrics/{rubricId}/Versions/{semver}/Retire")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RetireRubric(string rubricId, string semver, CancellationToken cancellationToken) =>
        RunCommandAsync(() => _rubrics.RetireAsync(rubricId, semver, cancellationToken));

    [HttpPost("Rubrics/{rubricId}/Versions/{semver}/Delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteRubricDraft(string rubricId, string semver, string? checksum, CancellationToken cancellationToken) =>
        RunCommandAsync(() => _rubrics.DeleteAsync(rubricId, semver, checksum, cancellationToken));

    private async Task<IActionResult> PostedEditorAsync(string mode, string rubricId, string semver, string title, CancellationToken cancellationToken)
    {
        Section("rubrics", title);
        var result = await _rubrics.PostEditorAsync(ReadEditor(mode, rubricId, semver), cancellationToken);
        if (result.Redirect is not null)
        {
            Temp(result.Action ?? ConfigurationAction.Ok("Draft saved."));
            return Redirect(result.Redirect);
        }

        return View("RubricEditor", result.Page);
    }

    private async Task<IActionResult> RunCommandAsync(Func<Task<RubricCommand>> command)
    {
        var result = await command();
        Temp(result.Action);
        return result.Redirect switch
        {
            "list" => RedirectToAction(nameof(Rubrics)),
            "detail" => RedirectToAction(nameof(Rubric), new { rubricId = result.RubricId }),
            _ => RedirectToAction(nameof(RubricVersion), new { rubricId = result.RubricId, semver = result.Semver })
        };
    }

    private RubricEditorPost ReadEditor(string mode, string rubricId, string semver)
    {
        var form = Request.Form;
        string Value(string name) => form[name].ToString();
        string[] Texts(string name) => form[name].Select(value => value ?? "").ToArray();
        return new RubricEditorPost
        {
            Intent = Value("intent"),
            DefinitionJson = Value("definitionJson"),
            BaselineJson = Value("baselineJson"),
            SavedChecksum = Value("savedChecksum"),
            SavedSemver = Value("savedSemver"),
            Mode = string.IsNullOrEmpty(Value("mode")) ? mode : Value("mode"),
            ExistingVersions = Value("existingVersions"),
            Id = Value("id"),
            Semver = Value("semver"),
            Title = Value("title"),
            Owner = Value("owner"),
            Dimensions = form["dimensions"].Select(value => value ?? "").ToArray(),
            ScoringType = Value("scoringType"),
            Rollup = Value("rollup"),
            ResourceType = Value("resourceType"),
            WorkflowTag = Value("workflowTag"),
            PasteText = Value("pasteText"),
            AddType = Value("addType"),
            CheckOpen = Value("checkOpen") == "1",
            EditIndex = int.TryParse(Value("editIndex"), out var index) ? index : -1,
            RouteRubricId = rubricId,
            RouteSemver = semver,
            Check = new RubricCheckPost
            {
                Id = Value("checkId"),
                Type = Value("checkType"),
                Dimension = Value("checkDimension"),
                Severity = Value("checkSeverity"),
                Enabled = form["checkEnabled"].ToString().Split(',').Any(part => part.Trim() == "true"),
                Expression = Value("expression"),
                FailureMessage = Value("failureMessage"),
                Code = Value("code"),
                Profiles = Value("profiles"),
                ValidateCodings = form["validateCodings"].ToString().Split(',').Any(part => part.Trim() == "true"),
                ValidateCodingsPosted = form.ContainsKey("validateCodingsPosted"),
                Whitelist = Value("whitelist"),
                Path = Value("path"),
                ValueSet = Value("valueSet"),
                System = Value("system"),
                MatchMode = Value("matchMode"),
                ChildId = Texts("childId"),
                ChildType = Texts("childType"),
                ChildExpression = Texts("childExpression"),
                ChildPath = Texts("childPath"),
                ChildValueSet = Texts("childValueSet"),
                ChildSystem = Texts("childSystem"),
                ChildFailure = Texts("childFailure"),
                ChildCode = Texts("childCode")
            }
        };
    }

    private static async Task<byte[]?> ReadUploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is not { Length: > 0 and <= RubricUploadBytes })
            return null;
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
    }
}
