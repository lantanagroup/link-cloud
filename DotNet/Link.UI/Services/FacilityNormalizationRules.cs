using System.Text;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Form rules for the facility-hub normalization operation editor. Payloads match the
/// Normalization operation API, including the resource types that copy-location operations add.
/// </summary>
public static class FacilityNormalizationRules
{
    public const int PageSize = 20;
    public const int MaxImportCharacters = 64 * 1024;
    public const int MaxImportUrls = 200;
    public const int MaxPasteRows = 200;
    public const int MaxVendorOwners = 40;
    public const int SequencePageSize = 200;
    public const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    public const string HslocName = "HSLOC Location Mapping";
    public const string HslocDescription =
        "Maps local Location codes to NHSN Healthcare Facility Patient Care Location (HSLOC) codes. Using this operation will also automatically enable CopyLocation operation and the CopyLocationAliasToTypeIteratively operation.";
    public const string CopyLocationName = "Copy Location Operation";
    public const string CopyLocationDescription =
        "Copies each Location Identifier 'System' and 'Value' fields into Location.Type as a CodeableConcept";
    public const string CopyAliasName = "Copy Location Alias to Type Iteratively Operation";
    public const string CopyAliasDescription =
        "Copies Location Alias fields into Location.Type as a CodeableConcept. This also copies all parent Locations' aliases in the partOf hierarchy.";

    public static readonly string[] OperationTypes =
    [
        "CopyProperty",
        "CodeMap",
        "HSLOCMap",
        "ConditionalTransform",
        "CopyLocation",
        "CopyLocationAliasToTypeIteratively",
        "RemoveExtensions"
    ];

    public static readonly (int Value, string Label)[] Operators =
    [
        (0, "Equal"),
        (1, "Greater than"),
        (2, "Greater than or equal"),
        (3, "Less than"),
        (4, "Less than or equal"),
        (5, "Not equal"),
        (6, "Exists"),
        (7, "Does not exist")
    ];

    public static string Label(string? operationType) =>
        string.IsNullOrWhiteSpace(operationType)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(operationType.Trim(), "([a-z0-9])([A-Z])", "$1 $2");

    public static string? CanonicalType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return OperationTypes.FirstOrDefault(type =>
            string.Equals(type, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static int ClampPage(int? page) => page is null or < 1 ? 1 : page.Value;

    public static NormalizationOperationInput Blank(string operationType)
    {
        var input = new NormalizationOperationInput
        {
            OperationType = operationType,
            MaxIterations = 15
        };

        switch (operationType)
        {
            case "CopyLocation":
                input.Name = CopyLocationName;
                input.Description = CopyLocationDescription;
                input.ResourceTypes = ["Location"];
                break;
            case "CopyLocationAliasToTypeIteratively":
                input.Name = CopyAliasName;
                input.Description = CopyAliasDescription;
                input.ResourceTypes = ["Location"];
                break;
            case "HSLOCMap":
                input.Name = HslocName;
                input.Description = HslocDescription;
                input.FhirPath = "type";
                input.ResourceTypes = ["Location"];
                input.Maps =
                [
                    new CodeSystemMapInput
                    {
                        TargetSystem = HslocSystem,
                        Entries = [new CodeMapEntryInput()]
                    }
                ];
                break;
            case "CodeMap":
                input.Maps = [new CodeSystemMapInput { Entries = [new CodeMapEntryInput()] }];
                break;
            case "ConditionalTransform":
                input.Conditions = [new ConditionInput()];
                break;
            case "RemoveExtensions":
                input.ExtensionUrls = [new ExtensionUrlInput()];
                break;
        }

        return WithBlanks(input);
    }

    public static NormalizationOperationInput WithBlanks(NormalizationOperationInput input)
    {
        var type = CanonicalType(input.OperationType);
        input.OperationType = type ?? input.OperationType;
        if (type == "ConditionalTransform")
            input.Conditions = WithBlankCondition(input.Conditions);
        if (type is "CodeMap" or "HSLOCMap")
            input.Maps = WithBlankMap(input.Maps, type == "HSLOCMap");
        if (type == "RemoveExtensions")
            input.ExtensionUrls = WithBlankUrl(input.ExtensionUrls);
        input.ResourceTypes ??= new List<string>();
        return input;
    }

    public static NormalizationOperationInput FromOperation(NormalizationOperationApiModel model)
    {
        var input = new NormalizationOperationInput
        {
            OperationId = model.Id.ToString(),
            OperationType = CanonicalType(model.OperationType) ?? model.OperationType,
            Name = model.Name,
            Description = model.Description,
            IsDisabled = model.IsDisabled,
            ResourceTypes = model.OperationResourceTypes
                .Select(row => row.Resource?.ResourceName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            VendorVersionIds = model.VendorPresets
                .Select(preset => preset.VendorVersionId)
                .Where(presetId => presetId != Guid.Empty)
                .Distinct()
                .Select(presetId => presetId.ToString())
                .ToList(),
            MaxIterations = 15
        };

        if (!TryReadJson(model.OperationJson, input))
            input.ParseFailed = true;

        return WithBlanks(input);
    }

    public static bool TryBuild(
        NormalizationOperationInput input,
        string facilityId,
        IReadOnlyCollection<string> resourceCatalog,
        out CreateNormalizationOperationRequestApiModel? request,
        out bool isDisabled,
        out string? error)
    {
        request = null;
        isDisabled = input.IsDisabled;
        var type = CanonicalType(input.OperationType);
        if (type is null)
        {
            error = "Choose an operation type.";
            return false;
        }

        if (input.ParseFailed)
        {
            error = "This operation could not be read, so it was not saved.";
            return false;
        }

        var name = input.Name?.Trim() ?? string.Empty;
        var description = input.Description?.Trim() ?? string.Empty;
        if (type == "HSLOCMap")
        {
            if (name.Length == 0)
                name = HslocName;
            if (description.Length == 0)
                description = HslocDescription;
        }

        if (name.Length == 0)
        {
            error = "Name is required.";
            return false;
        }

        if (name.Length > 200)
        {
            error = "Name must be 200 characters or fewer.";
            return false;
        }

        if (description.Length > 2000)
        {
            error = "Description must be 2000 characters or fewer.";
            return false;
        }

        if (!TryResources(input.ResourceTypes, resourceCatalog, type, out var resources, out error))
            return false;

        var details = new CreateNormalizationOperationDetailsApiModel
        {
            OperationType = type,
            Name = name,
            Description = description
        };

        switch (type)
        {
            case "CopyProperty":
                if (!TryPath(input.SourceFhirPath, "Source FHIR path", out var source, out error)
                    || !TryPath(input.TargetFhirPath, "Target FHIR path", out var target, out error))
                    return false;
                details.SourceFhirPath = source;
                details.TargetFhirPath = target;
                break;
            case "ConditionalTransform":
                if (!TryPath(input.TargetFhirPath, "Target FHIR path", out var transformPath, out error))
                    return false;
                var targetValue = input.TargetValue?.Trim() ?? string.Empty;
                if (targetValue.Length == 0)
                {
                    error = "Target value is required.";
                    return false;
                }

                if (targetValue.Length > 2000)
                {
                    error = "Target value must be 2000 characters or fewer.";
                    return false;
                }

                if (!TryConditions(input.Conditions, out var conditions, out error))
                    return false;
                details.TargetFhirPath = transformPath;
                details.TargetValue = targetValue;
                details.Conditions = conditions;
                break;
            case "CodeMap":
            case "HSLOCMap":
                if (type == "HSLOCMap")
                {
                    details.FhirPath = "type";
                }
                else if (!TryPath(input.FhirPath, "FHIR path", out var fhirPath, out error))
                {
                    return false;
                }
                else
                {
                    details.FhirPath = fhirPath;
                }

                if (!TryMaps(input.Maps, type == "HSLOCMap", out var maps, out error))
                    return false;
                details.CodeSystemMaps = maps;
                break;
            case "CopyLocation":
                break;
            case "CopyLocationAliasToTypeIteratively":
                if (input.MaxIterations is null)
                {
                    error = "Max iterations is required.";
                    return false;
                }

                if (input.MaxIterations is < 1 or > 1000)
                {
                    error = "Max iterations must be from 1 to 1000.";
                    return false;
                }

                details.MaxIterations = input.MaxIterations;
                details.SplitOnComma = input.SplitOnComma;
                break;
            case "RemoveExtensions":
                if (!TryUrls(input.ExtensionUrls, out var urls, out error))
                    return false;
                details.ExtensionUrls = urls;
                break;
        }

        request = new CreateNormalizationOperationRequestApiModel
        {
            FacilityId = facilityId,
            ResourceTypes = resources,
            Description = description,
            Operation = details,
            VendorVersionIds = []
        };
        error = null;
        return true;
    }

    public static bool TryBuildSequence(
        string? resourceType,
        IReadOnlyCollection<string> resourceCatalog,
        IEnumerable<NormalizationSequenceEntryInput>? rows,
        out List<CreateNormalizationOperationSequenceApiModel> sequences,
        out bool clear,
        out string? error)
    {
        sequences = [];
        clear = false;
        var canonical = resourceCatalog.FirstOrDefault(item =>
            string.Equals(item, resourceType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
        {
            error = "Choose a resource type for the sequence.";
            return false;
        }

        var seenIds = new HashSet<Guid>();
        var seenNumbers = new HashSet<int>();
        foreach (var row in rows ?? [])
        {
            if (row.OperationId == Guid.Empty)
            {
                error = "A sequence row is missing its operation.";
                return false;
            }

            if (row.Sequence is null)
                continue;

            if (row.Sequence <= 0)
            {
                error = "Sequence numbers start at 1.";
                return false;
            }

            if (!seenIds.Add(row.OperationId))
            {
                error = "Each operation can appear once in a sequence.";
                return false;
            }

            if (!seenNumbers.Add(row.Sequence.Value))
            {
                error = "Sequence numbers must be unique.";
                return false;
            }

            sequences.Add(new CreateNormalizationOperationSequenceApiModel
            {
                OperationId = row.OperationId,
                Sequence = row.Sequence
            });
        }

        sequences = sequences.OrderBy(row => row.Sequence).ToList();
        clear = sequences.Count == 0;
        error = null;
        return true;
    }

    public static bool TryTestResource(string? json, out string? normalized, out string? error)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Paste a FHIR resource to test.";
            return false;
        }

        if (json.Length > 1_000_000)
        {
            error = "The test resource is too large.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !HasProperty(doc.RootElement, "resourceType"))
            {
                error = "The test resource must be a JSON object with resourceType.";
                return false;
            }
        }
        catch (JsonException)
        {
            error = "The test resource is not valid JSON.";
            return false;
        }

        normalized = json.Trim();
        error = null;
        return true;
    }

    public static string Cap(string? text, int max = 12000)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? string.Empty;

        return text[..max] + "...";
    }

    private static bool TryResources(
        List<string>? posted,
        IReadOnlyCollection<string> catalog,
        string operationType,
        out List<string> resources,
        out string? error)
    {
        var selected = (posted ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (operationType == "HSLOCMap")
            selected = ["Location"];

        if (operationType is "CopyLocation" or "CopyLocationAliasToTypeIteratively"
            && !selected.Contains("Location", StringComparer.OrdinalIgnoreCase))
        {
            selected.Add("Location");
        }

        if (selected.Count == 0)
        {
            resources = [];
            error = "Select at least one resource type.";
            return false;
        }

        resources = new List<string>(selected.Count);
        foreach (var name in selected)
        {
            var match = catalog.FirstOrDefault(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                error = $"Resource type {name} is not a normalization resource type.";
                return false;
            }

            resources.Add(match);
        }

        error = null;
        return true;
    }

    private static bool TryPath(string? value, string label, out string path, out string? error)
    {
        path = value?.Trim() ?? string.Empty;
        if (path.Length == 0)
        {
            error = $"{label} is required.";
            return false;
        }

        if (path.Length > 2000)
        {
            error = $"{label} must be 2000 characters or fewer.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryConditions(
        List<ConditionInput>? rows,
        out List<CreateNormalizationConditionApiModel> conditions,
        out string? error)
    {
        conditions = [];
        foreach (var row in rows ?? [])
        {
            if (row.Remove)
                continue;

            var path = row.FhirPathSource?.Trim() ?? string.Empty;
            var value = row.Value?.Trim() ?? string.Empty;
            if (path.Length == 0 && value.Length == 0 && row.Operator == 0)
                continue;

            if (path.Length == 0)
            {
                error = "Each condition needs a FHIR path.";
                return false;
            }

            if (path.Length > 2000)
            {
                error = "A condition FHIR path must be 2000 characters or fewer.";
                return false;
            }

            if (row.Operator is < 0 or > 7)
            {
                error = "Choose a condition operator.";
                return false;
            }

            var valueOptional = row.Operator is 6 or 7;
            if (!valueOptional && value.Length == 0)
            {
                error = "Each condition needs a value, unless the operator is Exists or Does not exist.";
                return false;
            }

            if (value.Length > 2000)
            {
                error = "A condition value must be 2000 characters or fewer.";
                return false;
            }

            conditions.Add(new CreateNormalizationConditionApiModel
            {
                FhirPathSource = path,
                Operator = row.Operator,
                Value = value.Length == 0 ? null : value
            });
        }

        if (conditions.Count == 0)
        {
            error = "Add at least one condition.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryMaps(
        List<CodeSystemMapInput>? rows,
        bool hsloc,
        out List<CreateNormalizationCodeSystemMapApiModel> maps,
        out string? error)
    {
        maps = [];
        foreach (var row in rows ?? [])
        {
            if (row.Remove)
                continue;

            if (!TryParseCodeMapPaste(row.PasteRows, out var pasted, out error))
                return false;

            if (pasted.Count > 0)
            {
                row.Entries ??= [];
                if (row.Entries.Count > 0 && IsBlankEntry(row.Entries[^1]))
                    row.Entries.RemoveAt(row.Entries.Count - 1);
                row.Entries.AddRange(pasted);
                row.PasteRows = null;
            }

            var source = row.SourceSystem?.Trim() ?? string.Empty;
            var target = hsloc ? HslocSystem : row.TargetSystem?.Trim() ?? string.Empty;
            var entries = (row.Entries ?? [])
                .Where(entry => !entry.Remove)
                .ToList();
            var meaningful = entries.Where(entry => !IsBlankEntry(entry)).ToList();
            if (source.Length == 0 && meaningful.Count == 0 && (hsloc || target.Length == 0))
                continue;

            if (source.Length == 0 || target.Length == 0)
            {
                error = "Each code system map needs a source system and a target system.";
                return false;
            }

            if (source.Length > 2000 || target.Length > 2000)
            {
                error = "A code system must be 2000 characters or fewer.";
                return false;
            }

            var codeMaps = new Dictionary<string, CreateNormalizationCodeMapEntryApiModel>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (IsBlankEntry(entry))
                    continue;

                var key = entry.SourceCode?.Trim() ?? string.Empty;
                var code = entry.Code?.Trim() ?? string.Empty;
                var display = entry.Display?.Trim() ?? string.Empty;
                if (key.Length == 0 || code.Length == 0 || display.Length == 0)
                {
                    error = "Each code map needs a source code, a target code, and a display.";
                    return false;
                }

                if (key.Length > 200 || code.Length > 200 || display.Length > 500)
                {
                    error = "A code map entry is too long.";
                    return false;
                }

                if (!codeMaps.TryAdd(key, new CreateNormalizationCodeMapEntryApiModel { Code = code, Display = display }))
                {
                    error = $"Source code {key} is repeated in one code system map.";
                    return false;
                }
            }

            if (codeMaps.Count == 0)
            {
                error = "Each code system map needs at least one code.";
                return false;
            }

            maps.Add(new CreateNormalizationCodeSystemMapApiModel
            {
                SourceSystem = source,
                TargetSystem = target,
                CodeMaps = codeMaps
            });
        }

        if (maps.Count == 0)
        {
            error = "Add at least one code system map.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryUrls(List<ExtensionUrlInput>? rows, out List<string> urls, out string? error)
    {
        urls = [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows ?? [])
        {
            if (row.Remove)
                continue;

            var url = row.Url?.Trim() ?? string.Empty;
            if (url.Length == 0)
                continue;

            if (url.Length > 2000 || url.Any(char.IsWhiteSpace))
            {
                error = "Each extension URL must be a single token of 2000 characters or fewer.";
                return false;
            }

            if (!seen.Add(url))
            {
                error = $"Extension URL {url} is repeated.";
                return false;
            }

            urls.Add(url);
        }

        if (urls.Count == 0)
        {
            error = "Add at least one extension URL.";
            return false;
        }

        error = null;
        return true;
    }

    private static List<ConditionInput> WithBlankCondition(IEnumerable<ConditionInput>? rows)
    {
        var list = (rows ?? [])
            .Where(row => !row.Remove)
            .Select(row => new ConditionInput
            {
                FhirPathSource = row.FhirPathSource,
                Operator = row.Operator,
                Value = row.Value
            })
            .ToList();
        if (list.Count == 0 || !IsBlankCondition(list[^1]))
            list.Add(new ConditionInput());
        return list;
    }

    private static List<CodeSystemMapInput> WithBlankMap(IEnumerable<CodeSystemMapInput>? rows, bool hsloc)
    {
        var list = (rows ?? [])
            .Where(row => !row.Remove)
            .Select(row => new CodeSystemMapInput
            {
                SourceSystem = row.SourceSystem,
                TargetSystem = hsloc ? HslocSystem : row.TargetSystem,
                Entries = WithBlankEntry(row.Entries),
                PasteRows = row.PasteRows
            })
            .ToList();
        if (list.Count == 0 || !IsBlankMap(list[^1], hsloc))
        {
            list.Add(new CodeSystemMapInput
            {
                TargetSystem = hsloc ? HslocSystem : null,
                Entries = [new CodeMapEntryInput()]
            });
        }

        return list;
    }

    private static List<CodeMapEntryInput> WithBlankEntry(IEnumerable<CodeMapEntryInput>? rows)
    {
        var list = (rows ?? [])
            .Where(row => !row.Remove)
            .Select(row => new CodeMapEntryInput
            {
                SourceCode = row.SourceCode,
                Code = row.Code,
                Display = row.Display
            })
            .ToList();
        if (list.Count == 0 || !IsBlankEntry(list[^1]))
            list.Add(new CodeMapEntryInput());
        return list;
    }

    private static List<ExtensionUrlInput> WithBlankUrl(IEnumerable<ExtensionUrlInput>? rows)
    {
        var list = (rows ?? [])
            .Where(row => !row.Remove)
            .Select(row => new ExtensionUrlInput { Url = row.Url })
            .ToList();
        if (list.Count == 0 || !string.IsNullOrWhiteSpace(list[^1].Url))
            list.Add(new ExtensionUrlInput());
        return list;
    }

    private static bool IsBlankCondition(ConditionInput row) =>
        string.IsNullOrWhiteSpace(row.FhirPathSource)
        && string.IsNullOrWhiteSpace(row.Value)
        && row.Operator == 0;

    private static bool IsBlankEntry(CodeMapEntryInput row) =>
        string.IsNullOrWhiteSpace(row.SourceCode)
        && string.IsNullOrWhiteSpace(row.Code)
        && string.IsNullOrWhiteSpace(row.Display);

    private static bool IsBlankMap(CodeSystemMapInput row, bool hsloc)
    {
        var target = row.TargetSystem?.Trim() ?? string.Empty;
        var targetBlank = hsloc ? target.Length == 0 || string.Equals(target, HslocSystem, StringComparison.Ordinal) : target.Length == 0;
        var entriesBlank = (row.Entries ?? []).All(IsBlankEntry);
        return string.IsNullOrWhiteSpace(row.SourceSystem) && targetBlank && entriesBlank;
    }

    private static bool TryReadJson(string? json, NormalizationOperationInput input)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var root = doc.RootElement;
            input.Name = Text(root, "Name") ?? input.Name;
            input.Description = Text(root, "Description") ?? input.Description;
            input.SourceFhirPath = Text(root, "SourceFhirPath");
            input.TargetFhirPath = Text(root, "TargetFhirPath");
            input.FhirPath = Text(root, "FhirPath");
            input.TargetValue = Text(root, "TargetValue");
            if (TryInt(root, "MaxIterations", out var max))
                input.MaxIterations = max;
            if (TryBool(root, "SplitOnComma", out var split))
                input.SplitOnComma = split;

            if (Child(root, "Conditions") is { ValueKind: JsonValueKind.Array } conditions)
            {
                input.Conditions = conditions.EnumerateArray().Select(item => new ConditionInput
                {
                    FhirPathSource = Text(item, "FhirPathSource"),
                    Operator = ReadOperator(item),
                    Value = Text(item, "Value")
                }).ToList();
            }

            if (Child(root, "CodeSystemMaps") is { ValueKind: JsonValueKind.Array } maps)
            {
                input.Maps = maps.EnumerateArray().Select(map => new CodeSystemMapInput
                {
                    SourceSystem = Text(map, "SourceSystem"),
                    TargetSystem = Text(map, "TargetSystem"),
                    Entries = ReadEntries(map)
                }).ToList();
            }

            if (Child(root, "ExtensionUrls") is { ValueKind: JsonValueKind.Array } urls)
            {
                input.ExtensionUrls = urls.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Select(url => new ExtensionUrlInput { Url = url })
                    .ToList();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<CodeMapEntryInput> ReadEntries(JsonElement map)
    {
        if (Child(map, "CodeMaps") is not { ValueKind: JsonValueKind.Object } codes)
            return [];

        return codes.EnumerateObject().Select(property => new CodeMapEntryInput
        {
            SourceCode = property.Name,
            Code = property.Value.ValueKind == JsonValueKind.Object ? Text(property.Value, "Code") : null,
            Display = property.Value.ValueKind == JsonValueKind.Object ? Text(property.Value, "Display") : null
        }).ToList();
    }

    private static int ReadOperator(JsonElement item)
    {
        var value = Child(item, "Operator");
        if (value is null)
            return 0;

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var number))
            return number;

        if (value.Value.ValueKind == JsonValueKind.String)
        {
            var text = value.Value.GetString();
            var match = Operators.FirstOrDefault(op => string.Equals(op.Label.Replace(" ", string.Empty), text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(op.Value.ToString(), text, StringComparison.Ordinal));
            if (match != default || text == "0")
            {
                if (string.Equals(text, "Equal", StringComparison.OrdinalIgnoreCase) || text == "0")
                    return 0;
                if (match != default)
                    return match.Value;
            }

            return text switch
            {
                "GreaterThan" => 1,
                "GreaterThanOrEqual" => 2,
                "LessThan" => 3,
                "LessThanOrEqual" => 4,
                "NotEqual" => 5,
                "Exists" => 6,
                "NotExists" => 7,
                _ => 0
            };
        }

        return 0;
    }

    private static string? Text(JsonElement element, string name)
    {
        var child = Child(element, name);
        if (child is null || child.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return child.Value.ValueKind == JsonValueKind.String ? child.Value.GetString() : child.Value.ToString();
    }

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        var child = Child(element, name);
        return child is { ValueKind: JsonValueKind.Number } && child.Value.TryGetInt32(out value);
    }

    private static bool TryBool(JsonElement element, string name, out bool value)
    {
        value = false;
        var child = Child(element, name);
        if (child is not { ValueKind: JsonValueKind.True or JsonValueKind.False })
            return false;

        value = child.Value.GetBoolean();
        return true;
    }

    private static JsonElement? Child(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    private static bool HasProperty(JsonElement element, string name) => Child(element, name) is not null;

    public static bool TryVendorPresets(
        bool posted,
        bool vendorListLoaded,
        bool isUpdate,
        IEnumerable<string>? postedIds,
        IEnumerable<Guid> catalogIds,
        IEnumerable<Guid> existingIds,
        out List<Guid>? vendorVersionIds,
        out string? error)
    {
        vendorVersionIds = null;
        if (posted && !vendorListLoaded)
        {
            error = "Vendor versions could not be loaded, so the operation was not saved.";
            return false;
        }

        if (!posted)
        {
            vendorVersionIds = isUpdate ? null : [];
            error = null;
            return true;
        }

        var allowed = new HashSet<Guid>();
        foreach (var id in catalogIds.Concat(existingIds))
        {
            if (id != Guid.Empty)
                allowed.Add(id);
        }

        var chosen = new List<Guid>();
        foreach (var raw in postedIds ?? [])
        {
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text))
                continue;
            if (!Guid.TryParse(text, out var id) || id == Guid.Empty || !allowed.Contains(id))
            {
                error = "Each vendor preset must be a vendor version on this page.";
                return false;
            }

            if (!chosen.Contains(id))
                chosen.Add(id);
        }

        vendorVersionIds = chosen;
        error = null;
        return true;
    }

    public static bool TryVendorOwners(
        bool posted,
        bool loaded,
        IEnumerable<string>? postedIds,
        IEnumerable<Guid> catalogIds,
        IEnumerable<Guid> existingIds,
        out List<Guid>? ids,
        out string? error)
    {
        ids = null;
        if (!posted || !loaded)
        {
            error = "Vendor versions could not be loaded, so the operation was not saved.";
            return false;
        }

        var allowed = new HashSet<Guid>();
        foreach (var id in catalogIds.Concat(existingIds))
        {
            if (id != Guid.Empty)
                allowed.Add(id);
        }

        var chosen = new List<Guid>();
        foreach (var raw in postedIds ?? [])
        {
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text))
                continue;
            if (!Guid.TryParse(text, out var id) || id == Guid.Empty || !allowed.Contains(id))
            {
                error = "Each vendor preset must be a vendor version on this page.";
                return false;
            }

            if (!chosen.Contains(id))
                chosen.Add(id);
        }

        if (chosen.Count == 0)
        {
            error = "Select at least one vendor version.";
            return false;
        }

        if (chosen.Count > MaxVendorOwners)
        {
            error = "Select at most 40 vendor versions.";
            return false;
        }

        ids = chosen;
        error = null;
        return true;
    }

    public static bool TryParseCodeMapPaste(string? text, out List<CodeMapEntryInput> rows, out string? error)
    {
        rows = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            error = null;
            return true;
        }

        if (text.Length > MaxImportCharacters)
        {
            error = "Pasted code maps must be 64 KB or smaller.";
            return false;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var tsv = lines.Any(line => !string.IsNullOrWhiteSpace(line) && line.Contains('\t'));
        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            List<string> fields;
            if (tsv)
            {
                fields = rawLine.Split('\t').Select(field => field.Trim()).ToList();
            }
            else if (!TrySplitCsv(rawLine, out fields))
            {
                error = "A pasted row has an unmatched quote.";
                return false;
            }

            if (fields.Count < 2)
                continue;

            var source = fields[0].Trim().TrimStart('\uFEFF');
            var target = fields[1].Trim();
            if (source.Length == 0 || target.Length == 0)
                continue;

            var display = fields.Count >= 3 && !string.IsNullOrWhiteSpace(fields[2]) ? fields[2].Trim() : target;
            rows.Add(new CodeMapEntryInput
            {
                SourceCode = source,
                Code = target,
                Display = display
            });
            if (rows.Count > MaxPasteRows)
            {
                error = "Paste at most 200 code map rows.";
                return false;
            }
        }

        if (rows.Count == 0)
        {
            error = "No code map rows could be read. Each row needs a source code and a target code.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryParseExtensionCsv(
        string? text,
        IReadOnlyCollection<string> resourceCatalog,
        out IReadOnlyList<ExtensionUrlGroup> groups,
        out string? error)
    {
        groups = Array.Empty<ExtensionUrlGroup>();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Choose a CSV file.";
            return false;
        }

        if (text.Length > MaxImportCharacters)
        {
            error = "The CSV must be 64 KB or smaller.";
            return false;
        }

        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var headerChecked = false;
        var urlCount = 0;
        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;
            if (!TrySplitCsv(rawLine, out var fields))
            {
                error = "A CSV row has an unmatched quote.";
                return false;
            }

            if (fields.Count < 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
            {
                error = "Each row needs a resource type and an extension URL.";
                return false;
            }

            var typeField = fields[0].Trim().TrimStart('\uFEFF');
            var url = fields[1].Trim();
            var canonical = resourceCatalog.FirstOrDefault(item =>
                string.Equals(item, typeField, StringComparison.OrdinalIgnoreCase));
            if (!headerChecked)
            {
                headerChecked = true;
                if (typeField.Equals("resource type", StringComparison.OrdinalIgnoreCase) || canonical is null)
                    continue;
            }

            if (canonical is null)
            {
                error = $"Resource type {typeField} is not in the facility catalog.";
                return false;
            }

            if (url.Length > 2000 || url.Any(char.IsWhiteSpace))
            {
                error = "Each extension URL must be a single token of 2000 characters or fewer.";
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "Each extension URL must be an absolute http or https URL.";
                return false;
            }

            if (!grouped.TryGetValue(canonical, out var urls))
            {
                urls = [];
                grouped[canonical] = urls;
                order.Add(canonical);
            }

            if (urls.Contains(url, StringComparer.Ordinal))
                continue;
            if (urlCount >= MaxImportUrls)
            {
                error = "A CSV can import at most 200 extension URLs.";
                return false;
            }

            urls.Add(url);
            urlCount++;
        }

        if (order.Count == 0)
        {
            error = "The CSV has no extension URL rows.";
            return false;
        }

        groups = order.Select(type => new ExtensionUrlGroup(type, grouped[type])).ToList();
        error = null;
        return true;
    }

    public static bool HasExtensionConflict(
        IEnumerable<NormalizationOperationApiModel>? existing,
        IReadOnlyList<ExtensionUrlGroup> groups,
        out string? error)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var url in group.Urls)
                wanted.Add(ExtensionKey(group.ResourceType, url));
        }

        var hits = new List<string>();
        foreach (var model in existing ?? [])
        {
            if (!string.Equals(CanonicalType(model.OperationType), "RemoveExtensions", StringComparison.OrdinalIgnoreCase))
                continue;

            var input = FromOperation(model);
            if (input.ParseFailed)
            {
                error = "An existing remove-extensions operation could not be read, so nothing was imported.";
                return true;
            }

            foreach (var type in input.ResourceTypes ?? [])
            {
                foreach (var row in input.ExtensionUrls ?? [])
                {
                    var url = row.Url?.Trim();
                    if (string.IsNullOrEmpty(url))
                        continue;
                    var key = ExtensionKey(type, url);
                    if (wanted.Contains(key) && !hits.Contains(key, StringComparer.Ordinal))
                        hits.Add(type.Trim() + "::" + url);
                }
            }
        }

        if (hits.Count > 0)
        {
            error = "Import stopped because these extension URLs already exist: " + string.Join(", ", hits.Take(5)) + ".";
            return true;
        }

        error = null;
        return false;
    }

    private static string ExtensionKey(string resourceType, string url) =>
        resourceType.Trim().ToLowerInvariant() + "::" + url.Trim();

    private static bool TrySplitCsv(string line, out List<string> fields)
    {
        fields = [];
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (quoted)
            {
                if (character != '"')
                {
                    current.Append(character);
                    continue;
                }

                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                quoted = false;
                continue;
            }

            if (character == '"')
            {
                quoted = true;
                continue;
            }

            if (character == ',')
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (quoted)
            return false;

        fields.Add(current.ToString().Trim());
        return true;
    }
}

public sealed record ExtensionUrlGroup(string ResourceType, IReadOnlyList<string> Urls);
