using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class NormalizationsController(INormalizationStore store) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var operations = await store.GetAllOperationsAsync(ct);
        var sequences = await store.GetAllSequencesAsync(ct);
        var suites = await store.GetAllSuitesAsync(ct);

        ViewBag.Operations = operations;
        ViewBag.Sequences = sequences;
        ViewBag.Suites = suites;

        return View(operations);
    }

    // ===== Operations =====

    [HttpGet]
    public async Task<IActionResult> GetOperationJson(Guid id, CancellationToken ct)
    {
        var op = await store.GetOperationByIdAsync(id, ct);
        if (op == null) return NotFound();
        return Json(op);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOperation([FromBody] NormalizationOperationDefinition model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Operation name is required.");
        if (string.IsNullOrWhiteSpace(model.OperationType))
            return BadRequest("Operation type is required.");
        if (model.ResourceTypes.Count == 0)
            return BadRequest("At least one resource type is required.");

        var existing = await store.GetOperationByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(StatusCodes.Status403Forbidden, "System operations cannot be modified.");

        model.IsSystem = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpsertOperationAsync(model, ct);
        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOperation([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var op = await store.GetOperationByIdAsync(request.Id, ct);
        if (op == null) return NotFound();
        if (op.IsSystem)
            return StatusCode(StatusCodes.Status403Forbidden, "System operations cannot be deleted.");

        var sequences = await store.GetAllSequencesAsync(ct);
        var referencedBySequence = sequences.Any(s => s.Entries.Any(e => e.OperationId == request.Id));
        if (referencedBySequence)
            return Conflict("Operation is referenced by one or more sequences and cannot be deleted.");

        var suites = await store.GetAllSuitesAsync(ct);
        var referencedBySuite = suites.Any(s => s.OperationIds.Contains(request.Id));
        if (referencedBySuite)
            return Conflict("Operation is referenced by one or more suites and cannot be deleted.");

        await store.DeleteOperationAsync(request.Id, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneOperation([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var source = await store.GetOperationByIdAsync(request.Id, ct);
        if (source == null) return NotFound();

        var clone = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            OperationType = source.OperationType,
            ResourceTypes = [..source.ResourceTypes],
            SourceFhirPath = source.SourceFhirPath,
            TargetFhirPath = source.TargetFhirPath,
            ConditionTargetFhirPath = source.ConditionTargetFhirPath,
            ConditionTargetValue = source.ConditionTargetValue,
            Conditions = [..source.Conditions],
            CodeMapFhirPath = source.CodeMapFhirPath,
            CodeSystemMaps = [..source.CodeSystemMaps],
            ExtensionUrls = [..source.ExtensionUrls],
            IsSystem = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await store.UpsertOperationAsync(clone, ct);
        return Json(new { id = clone.Id });
    }

    // ===== Sequences =====

    [HttpGet]
    public async Task<IActionResult> GetSequenceJson(Guid id, CancellationToken ct)
    {
        var seq = await store.GetSequenceByIdAsync(id, ct);
        if (seq == null) return NotFound();
        return Json(seq);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSequence([FromBody] NormalizationSequenceDefinition model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Sequence name is required.");
        if (model.Entries.Count == 0)
            return BadRequest("At least one operation entry is required.");

        var existing = await store.GetSequenceByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(StatusCodes.Status403Forbidden, "System sequences cannot be modified.");

        model.IsSystem = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpsertSequenceAsync(model, ct);
        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSequence([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var seq = await store.GetSequenceByIdAsync(request.Id, ct);
        if (seq == null) return NotFound();
        if (seq.IsSystem)
            return StatusCode(StatusCodes.Status403Forbidden, "System sequences cannot be deleted.");

        var suites = await store.GetAllSuitesAsync(ct);
        var referencedBySuite = suites.Any(s => s.SequenceIds.Contains(request.Id));
        if (referencedBySuite)
            return Conflict("Sequence is referenced by one or more suites and cannot be deleted.");

        await store.DeleteSequenceAsync(request.Id, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneSequence([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var source = await store.GetSequenceByIdAsync(request.Id, ct);
        if (source == null) return NotFound();

        var clone = new NormalizationSequenceDefinition
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            Entries = source.Entries.Select(e => new NormalizationSequenceEntry { OperationId = e.OperationId, Sequence = e.Sequence }).ToList(),
            IsSystem = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await store.UpsertSequenceAsync(clone, ct);
        return Json(new { id = clone.Id });
    }

    // ===== Suites =====

    [HttpGet]
    public async Task<IActionResult> GetSuiteJson(Guid id, CancellationToken ct)
    {
        var suite = await store.GetSuiteByIdAsync(id, ct);
        if (suite == null) return NotFound();
        return Json(suite);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSuite(
    [FromBody] NormalizationSuiteDefinition model,
    CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Suite name is required.");

        var existing = await store.GetSuiteByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(
                StatusCodes.Status403Forbidden,
                "System suites cannot be modified.");

        var sequences = await store.GetAllSequencesAsync(ct);

        var selectedSequences = sequences
            .Where(s => model.SequenceIds.Contains(s.Id))
            .ToList();

        var operationOccurrences = new Dictionary<Guid, List<string>>();

        void AddOperationOccurrence(Guid operationId, string source)
        {
            if (!operationOccurrences.TryGetValue(operationId, out var sources))
            {
                sources = [];
                operationOccurrences[operationId] = sources;
            }

            sources.Add(source);
        }

        // Operations explicitly selected as standalone operations.
        foreach (var operationId in model.OperationIds)
        {
            AddOperationOccurrence(operationId, "Standalone Operations");
        }

        // Operations included through selected sequences.
        foreach (var sequence in selectedSequences)
        {
            foreach (var entry in sequence.Entries)
            {
                AddOperationOccurrence(
                    entry.OperationId,
                    $"sequence '{sequence.Name}'");
            }
        }

        var duplicateOperationIds = operationOccurrences
            .Where(x => x.Value.Count > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateOperationIds.Count > 0)
        {
            var operations = await store.GetAllOperationsAsync(ct);

            var operationNames = operations
                .ToDictionary(o => o.Id, o => o.Name);

            var duplicateMessages = duplicateOperationIds.Select(id =>
            {
                var name = operationNames.TryGetValue(id, out var operationName)
                    ? operationName
                    : id.ToString();

                var sources = string.Join(
                    " and ",
                    operationOccurrences[id].Distinct());

                return $"'{name}' ({sources})";
            });

            return BadRequest(
                $"Normalization suite cannot contain the same operation more than once. " +
                $"Duplicate operation(s): {string.Join(", ", duplicateMessages)}.");
        }

        // Existing suites preserve their current default flag. New suites may
        // carry an explicit initial IsDefault value from the caller.
        model.IsDefault = existing?.IsDefault ?? model.IsDefault;
        model.IsSystem = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;

        await store.UpsertSuiteAsync(model, ct);

        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSuite([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var suite = await store.GetSuiteByIdAsync(request.Id, ct);
        if (suite == null) return NotFound();
        if (suite.IsSystem)
            return StatusCode(StatusCodes.Status403Forbidden, "System suites cannot be deleted.");
        if (suite.IsDefault)
            return Conflict("Default suite cannot be deleted. Promote another suite first.");

        await store.DeleteSuiteAsync(request.Id, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneSuite([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var source = await store.GetSuiteByIdAsync(request.Id, ct);
        if (source == null) return NotFound();

        var clone = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            OperationIds = [..source.OperationIds],
            SequenceIds = [..source.SequenceIds],
            IsSystem = false,
            IsDefault = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await store.UpsertSuiteAsync(clone, ct);
        return Json(new { id = clone.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultSuite([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var suite = await store.GetSuiteByIdAsync(request.Id, ct);
        if (suite == null) return NotFound();

        await store.SetDefaultSuiteAsync(request.Id, ct);
        return Ok();
    }

}
