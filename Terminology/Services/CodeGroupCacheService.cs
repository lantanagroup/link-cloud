using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Terminology.Application.Models;
using Terminology.Application.Settings;
using Code = Terminology.Application.Models.Code;

namespace Terminology.Services;

public class CodeGroupCacheService(
    ILogger<CodeGroupCacheService> logger,
    IMemoryCache cache,
    IOptions<TerminologyConfig> terminologyConfig)
{
    private readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromMinutes(60)); // Adjust expiration as needed
    private readonly ConcurrentBag<CacheKey> _cacheKeys = new ConcurrentBag<CacheKey>();
    private readonly TerminologyConfig _terminologyConfig = terminologyConfig.Value;

    public void SetCodeGroup(CodeGroup codeGroup)
    {
        CacheKey key = new CacheKey(codeGroup);
        cache.Set(key.Key, codeGroup, _cacheOptions);
        
        if (!_cacheKeys.Contains(key))
            _cacheKeys.Add(key);
    }

    public CodeGroup GetCodeGroupById(CodeGroup.CodeGroupTypes type, string id, string? version = null)
    {
        CacheKey? key = null;
        
        if (version == null)
            key = _cacheKeys.Where(k => k.Type == type && k.Id == id).OrderBy(k => k.Version).FirstOrDefault();
        else
            key = _cacheKeys.FirstOrDefault(k => k.Type == type && k.Id == id && string.Equals(k.Version, version, StringComparison.CurrentCultureIgnoreCase));

        if (key == null)
            return null;
        
        cache.TryGetValue(key.Key, out CodeGroup? codeGroup);
        return codeGroup;
    }

    public CodeGroup GetCodeGroup(CodeGroup.CodeGroupTypes type, string url, string? version = null)
    {
        CacheKey? key = null;
        
        if (version == null)
            key = _cacheKeys.Where(k => k.Type == type && string.Equals(k.Url, url, StringComparison.CurrentCultureIgnoreCase)).OrderBy(k => k.Version).FirstOrDefault();
        else
            key = _cacheKeys.FirstOrDefault(k => k.Type == type && string.Equals(k.Url, url, StringComparison.CurrentCultureIgnoreCase) && string.Equals(k.Version, version, StringComparison.CurrentCultureIgnoreCase));
        
        if (key == null)
            return null;
        
        cache.TryGetValue(key.Key, out CodeGroup? codeGroup);
        return codeGroup;
    }

    public void ClearCache()
    {
        foreach (var key in _cacheKeys)
            cache.Remove(key.Key);
        _cacheKeys.Clear();
    }

    private async Task<CodeGroup> GetCodeGroup(string jsonFilePath)
    {
        CodeGroup codeGroup = new CodeGroup();
            
        // Read the JSON file and parse it as a FHIR resource
        var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
        codeGroup.Resource = new Hl7.Fhir.Serialization.FhirJsonParser().Parse<Resource>(jsonContent);

        if (codeGroup.Resource is CodeSystem codeSystem)
        {
            codeGroup.Id = codeSystem.Id;
            codeGroup.Type = CodeGroup.CodeGroupTypes.CodeSystem;
            codeGroup.Url = codeSystem.Url;
            codeGroup.Version = codeSystem.Version;
        }
        else if (codeGroup.Resource is ValueSet valueSet)
        {
            codeGroup.Id = valueSet.Id;
            codeGroup.Type = CodeGroup.CodeGroupTypes.ValueSet;
            codeGroup.Url = valueSet.Url;
            codeGroup.Version = valueSet.Version;
        }
        else 
        {
            logger.LogWarning("Resource type {Type} is not supported", codeGroup.Resource.TypeName);
            return null;
        }

        return codeGroup;
    }

    private void ProcessValueSetCsv(CodeGroup codeGroup, CsvReader csv)
    {
        var records = csv.GetRecords<CsvValueSetRecord>();
        string system = null;
        List<Code> systemCodes = null;
                
        foreach (var record in records)
        {
            string code = record.Code;
            string display = record.Display;
                    
            if (system == null || (!string.IsNullOrEmpty(record.System) && system != record.System))
            {
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
    }

    private void ProcessCodeSystemCsv(CodeGroup codeGroup, CsvReader csv)
    {
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
    }

    public async void LoadCache()
    {
        this.ClearCache();

        if (string.IsNullOrEmpty(_terminologyConfig.Path) || !Directory.Exists(_terminologyConfig.Path))
        {
            logger.LogWarning("Terminology path {Path} does not exist. Cannot populate cache.", _terminologyConfig.Path);
            return;
        }
        
        var directories = Directory.GetDirectories(_terminologyConfig.Path);

        foreach (var directory in directories)
        {
            var jsonFilePaths = Directory.GetFiles(directory, "*.json");
            var csvFilePaths = Directory.GetFiles(directory, "*.csv");
            
            if (jsonFilePaths.Length == 0 || csvFilePaths.Length == 0)
            {
                logger.LogWarning("Directory {Directory} does not contain a JSON or CSV file", directory);
                continue;
            }
            
            string jsonFilePath = jsonFilePaths[0];
            string csvFilePath = csvFilePaths[0];

            try
            {
                CodeGroup codeGroup = await this.GetCodeGroup(jsonFilePath);

                // Read the CSV file and extract "system", "code" and "display" values from each row
                using (var reader = new StreamReader(csvFilePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    switch (codeGroup.Type)
                    {
                        case CodeGroup.CodeGroupTypes.CodeSystem:
                            this.ProcessCodeSystemCsv(codeGroup, csv);
                            break;
                        case CodeGroup.CodeGroupTypes.ValueSet:
                            this.ProcessValueSetCsv(codeGroup, csv);
                            break;
                        default:
                            logger.LogWarning("Code group type {Type} is not supported", codeGroup.Type);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading code group from {JsonFilePath}", jsonFilePath);
            }
        }
    }
}