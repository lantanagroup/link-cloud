using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Terminology.Services;

/// <summary>
/// Service class responsible for managing and caching code groups.
/// </summary>
public class CodeGroupCacheService(
    ILogger<CodeGroupCacheService> logger,
    IMemoryCache cache,
    IOptions<TerminologyConfig> terminologyConfig) : ICodeGroupCacheService
{
    private static readonly Regex ScientificNotationPattern = new(
        @"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)[eE][+-]?\d+$",
        RegexOptions.CultureInvariant);

    private readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions();
    private readonly ConcurrentBag<CacheKey> _cacheKeys = new ConcurrentBag<CacheKey>();
    private readonly TerminologyConfig _terminologyConfig = terminologyConfig.Value;

    /// <summary>
    /// Determines whether the specified directory exists on the file system.
    /// </summary>
    /// <param name="path">The path to the directory whose existence is being checked.</param>
    /// <returns>A boolean value indicating whether the directory exists.</returns>
    protected internal virtual bool DirectoryExists(string path) => Directory.Exists(path);

    /// <summary>
    /// Retrieves the names of subdirectories that match the specified path.
    /// </summary>
    /// <param name="path">The path to the directory to search for subdirectories.</param>
    /// <returns>An array of directory names within the specified path.</returns>
    protected internal virtual string[] GetDirectories(string path) => Directory.GetDirectories(path);

    /// <summary>
    /// Retrieves the names of files that match the specified search pattern in a specified directory.
    /// </summary>
    /// <param name="path">The path to the directory to search.</param>
    /// <param name="searchPattern">The search string to match against the names of files in the directory.</param>
    /// <returns>An array of file names that match the search pattern in the specified directory.</returns>
    protected internal virtual string[] GetFiles(string path, string searchPattern) => Directory.GetFiles(path, searchPattern);

    /// <summary>
    /// Reads all text from a file asynchronously at the specified path.
    /// </summary>
    /// <param name="path">The file path from which to read the text.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entire content of the file as a string.</returns>
    protected internal virtual Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

    /// <summary>
    /// Retrieves a specific code group from the cache based on its type, identifier, and optional version.
    /// </summary>
    /// <param name="type">The type of the code group to retrieve (e.g., CodeSystem, ValueSet).</param>
    /// <param name="id">The unique identifier of the code group.</param>
    /// <param name="version">The version of the code group. If null, the latest version is retrieved.</param>
    /// <returns>The requested code group if it exists in the cache; otherwise, null.</returns>
    public virtual CodeGroup? GetCodeGroupById(CodeGroup.CodeGroupTypes type, string id, string? version = null)
    {
        CacheKey? key = null;

        if (version == null)
            key = _cacheKeys.Where(k => k.Type == type && k.Id == id).OrderByDescending(k => k.Version, Comparer<string>.Create(CompareVersions)).FirstOrDefault();
        else
            key = _cacheKeys.FirstOrDefault(k => k.Type == type && k.Id == id && string.Equals(k.Version, version, StringComparison.CurrentCultureIgnoreCase));

        if (key == null)
            return null;

        cache.TryGetValue(key.Key, out CodeGroup? codeGroup);
        return codeGroup;
    }

    /// <summary>
    /// Retrieves a specific code group from the cache based on its type, identifier, and optional version.
    /// </summary>
    /// <param name="type">The type of code group to retrieve (e.g., CodeSystem, ValueSet).</param>
    /// <param name="identifier">The unique identifier of the code group.</param>
    /// <param name="version">The version of the code group. If null, the latest version is retrieved.</param>
    /// <returns>The requested code group if it exists in the cache; otherwise, null.</returns>
    public CodeGroup? GetCodeGroup(CodeGroup.CodeGroupTypes type, string identifier, string? version = null)
    {
        // A canonical URL may carry a version suffix, e.g. "http://example.org/ValueSet/x|4.0.1".
        // Split it off so the URL matches the cached (unversioned) key, and treat the embedded
        // version as the requested version when one was not supplied explicitly.
        var pipeIndex = identifier.IndexOf('|');
        if (pipeIndex >= 0)
        {
            version ??= identifier[(pipeIndex + 1)..];
            identifier = identifier[..pipeIndex];
        }

        var byType = _cacheKeys.Where(k => k.Type == type).ToList();

        // Prefer a match on Url; fall back to a match on a secondary identifier.
        var candidates = byType
            .Where(k => string.Equals(k.Url, identifier, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = byType
                .Where(k => k.Identifiers.Any(i => string.Equals(i.Value, identifier, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // Prefer the requested version if it is loaded; otherwise fall back to the latest.
        CacheKey? key = null;
        if (version != null)
            key = candidates.FirstOrDefault(k => string.Equals(k.Version, version, StringComparison.CurrentCultureIgnoreCase));

        key ??= candidates.OrderByDescending(k => k.Version, Comparer<string>.Create(CompareVersions)).FirstOrDefault();

        if (key == null)
            return null;

        cache.TryGetValue(key.Key, out CodeGroup? codeGroup);
        return codeGroup;
    }

    /// <summary>
    /// Compares two version strings semantically (e.g. "4.0.10" &gt; "4.0.9") when both parse as
    /// dotted-numeric versions; otherwise falls back to a case-insensitive ordinal string comparison.
    /// </summary>
    private static int CompareVersions(string? a, string? b)
    {
        if (TryParseVersion(a, out var versionA) && TryParseVersion(b, out var versionB))
            return versionA.CompareTo(versionB);

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to parse a version string as a dotted-numeric <see cref="Version"/>. A bare component
    /// like "4" is padded to "4.0" since <see cref="Version"/> requires at least major.minor.
    /// </summary>
    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Contains('.') ? value : value + ".0";
        return Version.TryParse(normalized, out version!);
    }

    /// <summary>
    /// Retrieves all code groups of the specified type from the cache, ensuring only the latest version
    /// of each unique code group is included in the result.
    /// </summary>
    /// <param name="type">The type of code groups to retrieve (e.g., CodeSystem, ValueSet).</param>
    /// <returns>A list of code groups of the specified type, each containing only the latest version.</returns>
    public virtual List<CodeGroup> GetAllCodeGroups(CodeGroup.CodeGroupTypes type)
    {
        List<CodeGroup> codeGroups = _cacheKeys
            .Where(k => k.Type == type)
            .Select(k => cache.Get<CodeGroup>(k.Key))
            .Where(cg => cg != null)
            .OrderByDescending(cg => cg!.Version, Comparer<string?>.Create(CompareVersions))
            .ToList()!;

        // Remove all but the first duplicate by id (returning only the HEAD/latest version)
        codeGroups = codeGroups.GroupBy(cg => cg.Id)
            .Select(g => g.First())
            .ToList();

        return codeGroups;
    }

    /// <summary>
    /// Clears all items from the cache, including associated keys.
    /// Iterates through the stored cache keys, removing each item from the memory cache.
    /// After removing all items, the list of cache keys is cleared to ensure no residual references.
    /// </summary>
    public void ClearCache()
    {
        foreach (var key in _cacheKeys)
            cache.Remove(key.Key);
        _cacheKeys.Clear();
    }

    protected internal virtual void SetCodeGroup(CodeGroup codeGroup)
    {
        CacheKey urlKey = new CacheKey((CodeGroup.CodeGroupTypes)codeGroup.Type!, codeGroup.Url!, codeGroup.Version!, codeGroup.Id!, codeGroup.Identifiers);
        cache.Set(urlKey.Key, codeGroup, _cacheOptions);

        // Compare on the composite key rather than the instance: CacheKey overrides neither Equals nor
        // GetHashCode, so ConcurrentBag.Contains is reference equality and never matches. That was
        // harmless while LoadCache was the only caller (it clears the cache first), but replacing a
        // loaded code group calls this a second time for a key already in the bag, and each duplicate
        // lengthens the scans in GetCodeGroup/GetCodeGroupById/GetAllCodeGroups.
        if (!_cacheKeys.Any(k => string.Equals(k.Key, urlKey.Key, StringComparison.Ordinal)))
            _cacheKeys.Add(urlKey);
    }

    /// <summary>
    /// Replaces the codes of an already-cached code group with the contents of a CSV, preserving the
    /// FHIR resource metadata loaded from disk. See <see cref="ICodeGroupCacheService.ReplaceCodesFromCsv"/>.
    /// </summary>
    public virtual CodeGroup ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes type, string id, string? version, string csvContent)
    {
        var existing = GetCodeGroupById(type, id, version)
            ?? throw new KeyNotFoundException(
                $"No {type} found in the cache with id '{id}' and version '{version ?? "latest"}'");

        // Build a replacement rather than mutating the cached instance. ProcessValueSetCsv and
        // ProcessCodeSystemCsv append to Codes without clearing it, so reusing the cached group would
        // merge the old codes with the new ones. It is also what keeps this safe to call while requests
        // are being served: this service is a singleton and FhirService enumerates CodeGroup.Codes, so
        // an in-place edit could tear a reader's enumeration. The swap is the single cache.Set inside
        // SetCodeGroup, leaving every reader with either the whole old group or the whole new one.
        var replacement = new CodeGroup
        {
            Type = existing.Type,
            Id = existing.Id,
            Version = existing.Version,
            Name = existing.Name,
            Url = existing.Url,
            Identifiers = [.. existing.Identifiers],
            // Shared by reference on purpose: FhirService deep-copies the resource before attaching an
            // expansion and otherwise returns it untouched, and an upload must never alter the metadata.
            Resource = existing.Resource
        };

        using var reader = new StringReader(csvContent);
        // Mirrors LoadCache: the optional trailing status column means a missing field is not an error.
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
        using var csv = new CsvReader(reader, config);

        // Both Process*Csv methods call SetCodeGroup as their final statement and enumerate the CSV
        // lazily, so any parse failure throws before the cache is touched and the previously loaded
        // codes survive intact. There is deliberately no rollback here, and no pre-clearing.
        switch (type)
        {
            case CodeGroup.CodeGroupTypes.CodeSystem:
                ProcessCodeSystemCsv(replacement, csv);
                break;
            case CodeGroup.CodeGroupTypes.ValueSet:
                ProcessValueSetCsv(replacement, csv);
                break;
            default:
                throw new InvalidOperationException($"Code group type {type} is not supported");
        }

        logger.LogWarning(
            "Codes for {Type} {Id} version {Version} were replaced in memory from an uploaded CSV ({Count} codes). " +
            "This instance no longer matches the terminology files on disk until the cache is reloaded.",
            type,
            id.SanitizeForLog(),
            (existing.Version ?? "latest").SanitizeForLog(),
            replacement.Codes.Values.Sum(codes => codes.Count));

        return replacement;
    }

    private async Task<CodeGroup> GetCodeGroup(string jsonFilePath)
    {
        CodeGroup codeGroup = new CodeGroup();

        // Read the JSON file and parse it as a FHIR resource
        var jsonContent = await ReadAllTextAsync(jsonFilePath);
        codeGroup.Resource = new FhirJsonParser().Parse<Resource>(jsonContent);

        if (codeGroup.Resource is CodeSystem codeSystem)
        {
            codeGroup.Id = codeSystem.Id;
            codeGroup.Type = CodeGroup.CodeGroupTypes.CodeSystem;
            codeGroup.Url = codeSystem.Url;
            codeGroup.Version = codeSystem.Version;
            codeGroup.Identifiers = codeSystem.Identifier;
        }
        else if (codeGroup.Resource is ValueSet valueSet)
        {
            codeGroup.Id = valueSet.Id;
            codeGroup.Type = CodeGroup.CodeGroupTypes.ValueSet;
            codeGroup.Url = valueSet.Url;
            codeGroup.Version = valueSet.Version;
            codeGroup.Identifiers = valueSet.Identifier;
        }
        else
        {
            logger.LogWarning("Resource type {Type} is not supported", codeGroup.Resource.TypeName);
            throw new InvalidOperationException($"Resource type {codeGroup.Resource.TypeName} is not supported");
        }

        return codeGroup;
    }

    internal void ProcessValueSetCsv(CodeGroup codeGroup, CsvReader csv)
    {
        // Validate column count
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord;
        if (headers == null || headers.Length > 4 || headers.Length < 3)
        {
            throw new InvalidOperationException("ValueSet CSV must have 3 or 4 columns: system, code, display, and optionally status.");
        }

        // A 4-column file carries value set membership status; a 3-column file does not.
        // Members loaded with membership status are authoritative; members without it fall back to
        // the code system status when validated (see FhirService.ResolveCodeStatus).
        bool hasStatusColumn = headers.Length == 4;

        string? system = null;
        List<Code>? systemCodes = null;
        int scientificNotationCodeCount = 0;
        string scientificNotationCodeExamples = "";
        var statusParser = new CodeStatusParser();

        while (csv.Read())
        {
            string recordSystem = csv.GetField(0) ?? string.Empty;
            string code = csv.GetField(1) ?? string.Empty;
            string display = csv.GetField(2) ?? string.Empty;
            CodeStatus status = hasStatusColumn
                ? statusParser.Parse(csv.GetField(3), code)
                : CodeStatus.Active;

            if (ScientificNotationPattern.IsMatch(code))
            {
                scientificNotationCodeCount++;

                if (scientificNotationCodeExamples.Length < 100)
                    scientificNotationCodeExamples += (scientificNotationCodeExamples.Length > 0 ? ", " : "") + code;
            }

            if (system == null || (!string.IsNullOrEmpty(recordSystem) && system != recordSystem))
            {
                if (string.IsNullOrEmpty(recordSystem))
                    continue;

                system = recordSystem;
                if (!codeGroup.Codes.ContainsKey(system))
                {
                    systemCodes = new List<Code>();
                    codeGroup.Codes.Add(system, systemCodes);
                }
                else
                {
                    systemCodes = codeGroup.Codes[system];
                }
            }

            if (systemCodes == null)
            {
                logger.LogWarning("System codes list is null for code {Code}", code);
                continue;
            }

            if (hasStatusColumn)
            {
                systemCodes.Add(new ValueSetCode
                {
                    Value = code,
                    Display = display,
                    Status = status
                });
            }
            else
            {
                systemCodes.Add(new Code
                {
                    Value = code,
                    Display = display
                });
            }
        }

        LogScientificNotationWarning(scientificNotationCodeCount, codeGroup.Id, scientificNotationCodeExamples);
        LogInvalidStatusWarning(statusParser, codeGroup.Id);

        SetCodeGroup(codeGroup);
        logger.LogDebug("Value set {ValueSet} loaded with {Count} codes", codeGroup.Id, codeGroup.Codes.Values.SelectMany(c => c).Count());
    }

    internal void ProcessCodeSystemCsv(CodeGroup codeGroup, CsvReader csv)
    {
        if (codeGroup == null)
            throw new ArgumentNullException(nameof(codeGroup));

        if (string.IsNullOrEmpty(codeGroup.Url))
            throw new ArgumentException("Code system URL is required", nameof(codeGroup));

        // Validate column count
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord;
        if (headers == null || headers.Length > 3 || headers.Length < 2)
        {
            throw new InvalidOperationException($"CodeSystem CSV must have exactly 2 or 3 columns: code, display, and optionally status.");
        }

        var records = csv.GetRecords<CsvCodeSystemRecord>();
        string system = codeGroup.Url;
        int scientificNotationCodeCount = 0;
        string scientificNotationCodeExamples = "";
        var statusParser = new CodeStatusParser();

        foreach (var record in records)
        {
            string code = record.Code;
            string display = record.Display;
            CodeStatus status = statusParser.Parse(record.Status, code);

            if (ScientificNotationPattern.IsMatch(code))
            {
                scientificNotationCodeCount++;

                if (scientificNotationCodeExamples.Length < 100)
                    scientificNotationCodeExamples += (scientificNotationCodeExamples.Length > 0 ? ", " : "") + code;
            }

            if (!codeGroup.Codes.ContainsKey(system))
                codeGroup.Codes.Add(system, new List<Code>());

            codeGroup.Codes[system].Add(new CodeSystemCode
            {
                Value = code,
                Display = display,
                Status = status
            });
        }

        LogScientificNotationWarning(scientificNotationCodeCount, codeGroup.Id, scientificNotationCodeExamples);
        LogInvalidStatusWarning(statusParser, codeGroup.Id);

        SetCodeGroup(codeGroup);
        logger.LogDebug("Code system {CodeSystem} loaded with {Count} codes", codeGroup.Id, codeGroup.Codes[system].Count);
    }

    private void LogScientificNotationWarning(int scientificNotationCodeCount, string? codeGroupId, string? examples)
    {
        if (scientificNotationCodeCount > 0)
        {
            logger.LogWarning(
                "Found {Count} code(s) in code group {CodeGroupId} that appear to be in scientific notation. Verify that codes were not altered during CSV creation. Example codes: {Examples}",
                scientificNotationCodeCount,
                codeGroupId.SanitizeForLog(),
                examples.SanitizeForLog());
        }
    }

    /// <summary>
    /// Reports status cells that could not be understood, once per file rather than once per row.
    /// </summary>
    private void LogInvalidStatusWarning(CodeStatusParser statusParser, string? codeGroupId)
    {
        if (statusParser.InvalidCount > 0)
        {
            logger.LogWarning(
                "Found {Count} code(s) in code group {CodeGroupId} with an unrecognized status; each was loaded as Active. Valid values are 'Active' and 'Inactive'. Example codes: {Examples}",
                statusParser.InvalidCount,
                codeGroupId.SanitizeForLog(),
                statusParser.Examples.SanitizeForLog());
        }
    }

    /// <summary>
    /// Parses the optional trailing status column shared by CodeSystem and ValueSet CSVs, tallying the
    /// values it could not understand so one warning can be logged per file.
    /// </summary>
    /// <remarks>
    /// The tally exists because these loops run once per code, and a large code system carries hundreds of
    /// thousands of them — a per-row warning on a malformed file would flood Loki.
    /// </remarks>
    private sealed class CodeStatusParser
    {
        private const int MaxExampleLength = 100;

        private string _examples = string.Empty;

        /// <summary>The number of rows whose status cell could not be parsed.</summary>
        internal int InvalidCount { get; private set; }

        /// <summary>A truncated, comma-separated sample of the offending codes and their raw values.</summary>
        internal string Examples => _examples;

        /// <summary>
        /// Resolves one status cell. A blank cell means "not stated" and yields Active, matching the
        /// 2- and 3-column forms that predate the status column. A value that is neither status name
        /// also yields Active, but is tallied.
        /// </summary>
        /// <remarks>
        /// Defaulting rather than rejecting is deliberate. Statuses are read into a strongly typed
        /// enum, and letting a converter throw on a bad cell aborts the whole file: the enclosing
        /// <see cref="LoadCache"/> catch would drop every code in the group — tens of thousands of good
        /// rows — over one typo, and the group would then answer 404 rather than anything diagnosable.
        /// One code loading as Active with a warning attached is the far smaller failure.
        /// </remarks>
        internal CodeStatus Parse(string? rawStatus, string code)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return CodeStatus.Active;
            }

            // IsDefined rejects the out-of-range numerics TryParse otherwise accepts ("7" would
            // parse to (CodeStatus)7 and compare equal to neither status).
            if (Enum.TryParse<CodeStatus>(rawStatus, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                return parsed;
            }

            InvalidCount++;

            if (_examples.Length < MaxExampleLength)
            {
                _examples += (_examples.Length > 0 ? ", " : "") + $"{code}='{rawStatus}'";
            }

            return CodeStatus.Active;
        }
    }

    /// <summary>
    /// Loads the code groups into the cache by processing JSON and CSV files
    /// located in the configured directory. Clears the existing cache before reloading.
    /// Logs the number of successfully loaded code groups and warnings for directories
    /// that could not be processed.
    /// </summary>
    public async Task LoadCache()
    {
        this.ClearCache();

        if (string.IsNullOrEmpty(_terminologyConfig.Path) || !this.DirectoryExists(_terminologyConfig.Path))
        {
            logger.LogWarning("Terminology path {Path} does not exist. Cannot populate cache.", _terminologyConfig.Path);
            return;
        }

        var directories = GetDirectories(_terminologyConfig.Path);
        int loadedValueSets = 0;
        int loadedCodeSystems = 0;
        List<string> notLoadedDirectories = new List<string>();

        foreach (var directory in directories)
        {
            var jsonFilePaths = GetFiles(directory, "*.json");
            var csvFilePaths = GetFiles(directory, "*.csv");

            logger.LogDebug("Loading code group from {Directory}", directory);

            if (jsonFilePaths.Length == 0 || csvFilePaths.Length == 0)
            {
                logger.LogWarning("Directory {Directory} does not contain a JSON or CSV file", directory);
                notLoadedDirectories.Add(directory);
                continue;
            }

            string jsonFilePath = jsonFilePaths[0];
            string csvFilePath = csvFilePaths[0];

            try
            {
                CodeGroup codeGroup = await this.GetCodeGroup(jsonFilePath);

                // Read the CSV file and extract "system", "code" and "display" values from each row
                var csvContent = await ReadAllTextAsync(csvFilePath);
                using var reader = new StringReader(csvContent);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture);

                // Both CodeSystem and ValueSet CSVs have an optional trailing status column, so a
                // missing field is tolerated (a 3-column value set has no Status field to map).
                if (codeGroup.Type == CodeGroup.CodeGroupTypes.CodeSystem ||
                    codeGroup.Type == CodeGroup.CodeGroupTypes.ValueSet)
                    config.MissingFieldFound = null;

                using var csv = new CsvReader(reader, config);
                switch (codeGroup.Type)
                {
                    case CodeGroup.CodeGroupTypes.CodeSystem:
                        logger.LogDebug("Processing code system CSV for {CodeSystem}", codeGroup.Id);
                        this.ProcessCodeSystemCsv(codeGroup, csv);
                        loadedCodeSystems++;
                        break;
                    case CodeGroup.CodeGroupTypes.ValueSet:
                        logger.LogDebug("Processing value set CSV for {ValueSet}", codeGroup.Id);
                        this.ProcessValueSetCsv(codeGroup, csv);
                        loadedValueSets++;
                        break;
                    default:
                        logger.LogWarning("Code group type {Type} is not supported", codeGroup.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading code group from {csvFilePath}", csvFilePath);
            }
        }

        logger.LogInformation("Loaded {LoadedValueSetsCount} value sets and {LoadedCodeSystemsCount} code systems for a total of {AllCodeGroupsCount} code groups.", loadedValueSets, loadedCodeSystems, loadedValueSets + loadedCodeSystems);

        if (notLoadedDirectories.Count > 0)
            logger.LogWarning("{NotLoadedCount} code groups were not loaded from the directory:\n- {NotLoadedList}", notLoadedDirectories.Count, String.Join("\n- ", notLoadedDirectories));
    }
}
