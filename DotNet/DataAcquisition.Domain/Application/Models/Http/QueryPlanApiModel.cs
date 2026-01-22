using LantanaGroup.Link.DataAcquisition.Domain.Application.Attributes;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;

public class QueryPlanApiModel : IValidatableObject
{
    [DataMember]
    [Required(ErrorMessage = "PlanName is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "PlanName must be between 1 and 200 characters.")]
    public string? PlanName { get; set; }

    [Required(ErrorMessage = "Type is required.")]
    [DataMember]
    public required Frequency? Type { get; set; }

    [Required(ErrorMessage = "FacilityId is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "FacilityId must be between 1 and 100 characters.")]
    [DataMember]
    public required string? FacilityId { get; set; }

    [DataMember]
    [StringLength(500, ErrorMessage = "EHRDescription cannot exceed 500 characters.")]
    public string? EHRDescription { get; set; }

    [DataMember]
    [StringLength(50, ErrorMessage = "LookBack cannot exceed 50 characters.")]
    public string? LookBack { get; set; }

    [DataMember]
    [Required(ErrorMessage = "InitialQueries is required.")]
    [MinDictionaryCount(1, ErrorMessage = "InitialQueries must contain at least one query configuration.")]
    [ValidateQueryConfigDictionary]
    public Dictionary<string, IQueryConfig> InitialQueries { get; set; } = new();

    [DataMember]
    [Required(ErrorMessage = "SupplementalQueries is required.")]
    [MinDictionaryCount(1, ErrorMessage = "SupplementalQueries must contain at least one query configuration.")]
    [ValidateQueryConfigDictionary]
    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; } = new();

    [IgnoreDataMember, JsonIgnore]
    public DateTime? CreateDate { get; set; }

    [IgnoreDataMember, JsonIgnore]
    public DateTime? ModifyDate { get; set; }

    /// <summary>
    /// Performs additional validation logic that requires access to multiple properties
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate that dictionary keys are parseable as integers (for proper ordering)
        ValidateDictionaryKeys(InitialQueries, nameof(InitialQueries), results);
        ValidateDictionaryKeys(SupplementalQueries, nameof(SupplementalQueries), results);

        // Validate that there are no duplicate ResourceTypes within InitialQueries
        ValidateUniqueResourceTypes(InitialQueries, nameof(InitialQueries), results);

        // Validate LookBack format if provided
        if (!string.IsNullOrWhiteSpace(LookBack))
        {
            if (!IsValidLookBackFormat(LookBack))
            {
                results.Add(new ValidationResult(
                    "LookBack must be in a valid ISO 8601 duration format (e.g., 'P30D', 'P1Y', 'P0D', 'PT2H', 'P1Y2M3DT4H5M6S').",
                    new[] { nameof(LookBack) }));
            }
        }

        return results;
    }

    private void ValidateDictionaryKeys(
        Dictionary<string, IQueryConfig>? dictionary,
        string propertyName,
        List<ValidationResult> results)
    {
        if (dictionary == null) return;

        var nonNumericKeys = new List<string>();
        var seenKeys = new HashSet<int>();

        foreach (var key in dictionary.Keys)
        {
            if (!int.TryParse(key, out int numericKey))
            {
                nonNumericKeys.Add(key);
            }
            else
            {
                if (seenKeys.Contains(numericKey))
                {
                    results.Add(new ValidationResult(
                        $"{propertyName} contains duplicate numeric key: {key}",
                        new[] { propertyName }));
                }
                else
                {
                    seenKeys.Add(numericKey);
                }
            }
        }

        if (nonNumericKeys.Any())
        {
            results.Add(new ValidationResult(
                $"{propertyName} contains non-numeric keys which may cause ordering issues: {string.Join(", ", nonNumericKeys)}. " +
                "Consider using sequential numeric keys (0, 1, 2, etc.) for predictable query ordering.",
                new[] { propertyName }));
        }
    }

    private void ValidateUniqueResourceTypes(
        Dictionary<string, IQueryConfig>? dictionary,
        string propertyName,
        List<ValidationResult> results)
    {
        if (dictionary == null) return;

        var resourceTypeCounts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in dictionary)
        {
            var resourceType = kvp.Value switch
            {
                Infrastructure.Models.QueryConfig.ParameterQueryConfig paramConfig => paramConfig.ResourceType,
                Infrastructure.Models.QueryConfig.ReferenceQueryConfig refConfig => refConfig.ResourceType,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                if (!resourceTypeCounts.ContainsKey(resourceType))
                {
                    resourceTypeCounts[resourceType] = new List<string>();
                }
                resourceTypeCounts[resourceType].Add(kvp.Key);
            }
        }

        var duplicates = resourceTypeCounts.Where(kvp => kvp.Value.Count > 1).ToList();
        if (duplicates.Any())
        {
            foreach (var duplicate in duplicates)
            {
                results.Add(new ValidationResult(
                    $"{propertyName} contains duplicate ResourceType '{duplicate.Key}' in keys: {string.Join(", ", duplicate.Value)}. " +
                    "This may cause unexpected behavior during query execution.",
                    new[] { propertyName }));
            }
        }
    }

    private bool IsValidLookBackFormat(string lookBack)
    {
        // Validate ISO 8601 Duration Format
        // Format: P[n]Y[n]M[n]DT[n]H[n]M[n]S or P[n]W
        // Examples: P1Y, P3M, P0D, PT2H30M, P1Y2M3DT4H5M6S, P4W

        if (string.IsNullOrWhiteSpace(lookBack)) return false;

        // Must start with 'P'
        if (!lookBack.StartsWith("P", StringComparison.OrdinalIgnoreCase)) return false;

        // Minimum valid format is P0D, P1W, PT0S, etc.
        if (lookBack.Length < 2) return false;

        try
        {
            // Try to parse as TimeSpan using XmlConvert (built-in ISO 8601 parser)
            System.Xml.XmlConvert.ToTimeSpan(lookBack);
            return true;
        }
        catch
        {
            // If XmlConvert fails, do a more lenient regex check
            // This allows for valid ISO 8601 durations that might not parse to TimeSpan
            var iso8601Pattern = @"^P(?:\d+Y)?(?:\d+M)?(?:\d+W)?(?:\d+D)?(?:T(?:\d+H)?(?:\d+M)?(?:\d+(?:\.\d+)?S)?)?$";
            return System.Text.RegularExpressions.Regex.IsMatch(lookBack, iso8601Pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }

    /// <summary>
    /// Legacy validation method for backward compatibility.
    /// Consider using IValidatableObject.Validate() instead.
    /// </summary>
    [Obsolete("Use IValidatableObject.Validate() instead. This method will be removed in a future version.")]
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(this.PlanName))
            throw new ArgumentNullException(nameof(this.PlanName), "PlanName cannot be null or empty.");
        if (this.Type is null)
            throw new ArgumentNullException(nameof(this.Type), "Type is required.");
        if (string.IsNullOrWhiteSpace(this.FacilityId))
            throw new ArgumentNullException(nameof(this.FacilityId), "FacilityId is required.");
        if (this.InitialQueries is null || !this.InitialQueries.Any())
            throw new ArgumentNullException(nameof(this.InitialQueries), "InitialQueries is required.");
        if (this.SupplementalQueries is null || !this.SupplementalQueries.Any())
            throw new ArgumentNullException(nameof(this.SupplementalQueries), "SupplementalQueries is required.");

        return true;
    }
}