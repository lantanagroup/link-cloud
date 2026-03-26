using System.Collections.Concurrent;
using System.Globalization;
using CsvHelper;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;

namespace LantanaGroup.Link.Terminology.Services;

/// <summary>
/// Service class responsible for managing and caching code groups.
/// </summary>
public class CodeGroupCacheService(
    ILogger<CodeGroupCacheService> logger,
    IMemoryCache cache,
    IOptions<TerminologyConfig> terminologyConfig)
{
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
            key = _cacheKeys.Where(k => k.Type == type && k.Id == id).OrderByDescending(k => k.Version).FirstOrDefault();
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
        CacheKey? key = null;

        if (version == null)
        {
            key = _cacheKeys
                .Where(k => k.Type == type)
                .Where(k => string.Equals(k.Url, identifier, StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(k => k.Version)
                .FirstOrDefault();

            if (key == null)
            {
                key = _cacheKeys
                    .Where(k => k.Type == type)
                    .Where(k => k.Identifiers.Any(i => string.Equals(i.Value, identifier, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderByDescending(k => k.Version)
                    .FirstOrDefault();
            }
        }
        else
        {
            
            var keys = _cacheKeys
                .Where(k => k.Type == type)
                .Where(k => string.Equals(k.Version, version, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            key = keys
                .FirstOrDefault(k => string.Equals(k.Url, identifier, StringComparison.CurrentCultureIgnoreCase));

            if (key == null)
                key = keys.FirstOrDefault(k =>
                    k.Identifiers.Any(i =>
                        string.Equals(i.Value, identifier, StringComparison.CurrentCultureIgnoreCase)));
        }
        
        if (key == null)
            return null;
        
        cache.TryGetValue(key.Key, out CodeGroup? codeGroup);
        return codeGroup;
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
            .OrderByDescending(cg => cg!.Version)
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
        CacheKey urlKey = new CacheKey((CodeGroup.CodeGroupTypes) codeGroup.Type!, codeGroup.Url!, codeGroup.Version!, codeGroup.Id!, codeGroup.Identifiers);
        cache.Set(urlKey.Key, codeGroup, _cacheOptions);
        
        if (!_cacheKeys.Contains(urlKey))
            _cacheKeys.Add(urlKey);
    }

    private async Task<CodeGroup> GetCodeGroup(string jsonFilePath)
    {
        CodeGroup codeGroup = new CodeGroup();
            
        // Read the JSON file and parse it as a FHIR resource
        var jsonContent = await ReadAllTextAsync(jsonFilePath);
        codeGroup.Resource = new Hl7.Fhir.Serialization.FhirJsonParser().Parse<Resource>(jsonContent);

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
        if (headers == null || headers.Length != 3)
        {
            throw new InvalidOperationException("ValueSet CSV must have exactly 3 columns: code, display, and system");
        }

        var records = csv.GetRecords<CsvValueSetRecord>();
        string? system = null;
        List<Code>? systemCodes = null;
                
        foreach (var record in records)
        {
            string code = record.Code;
            string display = record.Display;
                    
            if (system == null || (!string.IsNullOrEmpty(record.System) && system != record.System))
            {
                if (string.IsNullOrEmpty(record.System))
                    continue;
                
                system = record.System;
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
                    
            systemCodes.Add(new Code
            {
                Value = code,
                Display = display
            });
        }
                    
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
        if (headers == null || headers.Length != 2)
        {
            throw new InvalidOperationException("CodeSystem CSV must have exactly 2 columns: code and display");
        }

        var records = csv.GetRecords<CsvCodeSystemRecord>();
        string system = codeGroup.Url;
        
        foreach (var record in records)
        {
            string code = record.Code;
            string display = record.Display;
            
            if (!codeGroup.Codes.ContainsKey(system))
                codeGroup.Codes.Add(system, new List<Code>());
            
            codeGroup.Codes[system].Add(new Code
            {
                Value = code,
                Display = display
            });
        }
            
        SetCodeGroup(codeGroup);
        logger.LogDebug("Code system {CodeSystem} loaded with {Count} codes", codeGroup.Id, codeGroup.Codes[system].Count);
    }

    /// <summary>
    /// Loads the code groups into the cache by processing JSON and CSV files
    /// located in the configured directory. Clears the existing cache before reloading.
    /// Logs the number of successfully loaded code groups and warnings for directories
    /// that could not be processed.
    /// </summary>
    public async System.Threading.Tasks.Task LoadCache()
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
                using (var reader = new StringReader(csvContent))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
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