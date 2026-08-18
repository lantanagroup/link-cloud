using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;

public interface IQueryPlanValidator
{
    ValidationResult ValidateQueryPlan(
        Dictionary<string, IQueryConfig>? initialQueries,
        Dictionary<string, IQueryConfig>? supplementalQueries);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public string GetErrorMessage()
    {
        if (IsValid) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Query Plan Validation Failed:");

        if (Errors.Any())
        {
            sb.AppendLine("Errors:");
            foreach (var error in Errors)
            {
                sb.AppendLine($"  - {error}");
            }
        }

        if (Warnings.Any())
        {
            sb.AppendLine("Warnings:");
            foreach (var warning in Warnings)
            {
                sb.AppendLine($"  - {warning}");
            }
        }

        return sb.ToString();
    }
}

public class QueryPlanValidator : IQueryPlanValidator
{
    private const int MaxKeyLength = 50;
    private const int MaxResourceTypeLength = 100;
    private const int MaxParameterNameLength = 100;

    public ValidationResult ValidateQueryPlan(
        Dictionary<string, IQueryConfig>? initialQueries,
        Dictionary<string, IQueryConfig>? supplementalQueries)
    {
        var result = new ValidationResult { IsValid = true };

        // Validate InitialQueries
        if (initialQueries == null || !initialQueries.Any())
        {
            result.IsValid = false;
            result.Errors.Add("InitialQueries cannot be null or empty.");
        }
        else
        {
            ValidateQueryDictionary(initialQueries, "InitialQueries", result);
        }

        // Validate SupplementalQueries
        if (supplementalQueries == null || !supplementalQueries.Any())
        {
            result.IsValid = false;
            result.Errors.Add("SupplementalQueries cannot be null or empty.");
        }
        else
        {
            ValidateQueryDictionary(supplementalQueries, "SupplementalQueries", result);
        }

        // Cross-reference validation between InitialQueries and SupplementalQueries
        if (initialQueries != null && supplementalQueries != null)
        {
            ValidateCrossReferences(initialQueries, supplementalQueries, result);
        }

        return result;
    }

    private void ValidateQueryDictionary(
        Dictionary<string, IQueryConfig> queries,
        string querySetName,
        ValidationResult result)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in queries)
        {
            var key = kvp.Key;
            var config = kvp.Value;

            // Validate key
            ValidateKey(key, querySetName, seenKeys, result);

            // Validate config is not null
            if (config == null)
            {
                result.IsValid = false;
                result.Errors.Add($"{querySetName}['{key}']: Query configuration cannot be null.");
                continue;
            }

            // Validate based on query type
            switch (config)
            {
                case ParameterQueryConfig paramConfig:
                    ValidateParameterQueryConfig(paramConfig, key, querySetName, resourceTypes, result);
                    break;
                case ReferenceQueryConfig refConfig:
                    ValidateReferenceQueryConfig(refConfig, key, querySetName, result);
                    break;
                default:
                    result.IsValid = false;
                    result.Errors.Add($"{querySetName}['{key}']: Unknown query config type '{config.GetType().Name}'.");
                    break;
            }
        }

        // Validate query order (ParameterQueryConfig must come before ReferenceQueryConfig)
        ValidateQueryOrder(queries, querySetName, result);
    }

    private void ValidateKey(
        string key,
        string querySetName,
        HashSet<string> seenKeys,
        ValidationResult result)
    {
        // Check for null or empty
        if (string.IsNullOrWhiteSpace(key))
        {
            result.IsValid = false;
            result.Errors.Add($"{querySetName}: Dictionary key cannot be null or whitespace.");
            return;
        }

        // Check length
        if (key.Length > MaxKeyLength)
        {
            result.IsValid = false;
            result.Errors.Add($"{querySetName}['{key}']: Key length exceeds maximum of {MaxKeyLength} characters.");
        }

        // Check for duplicates (case-insensitive)
        if (seenKeys.Contains(key))
        {
            result.IsValid = false;
            result.Errors.Add($"{querySetName}['{key}']: Duplicate key detected (keys are case-insensitive).");
        }
        else
        {
            seenKeys.Add(key);
        }

        // Validate key is numeric (for ordering)
        if (!int.TryParse(key, out int keyValue))
        {
            result.Warnings.Add($"{querySetName}['{key}']: Key is not numeric. Query ordering may not work as expected.");
        }
        else if (keyValue < 0)
        {
            result.Warnings.Add($"{querySetName}['{key}']: Key is negative. Consider using positive integers for clarity.");
        }
    }

    private void ValidateParameterQueryConfig(
        ParameterQueryConfig config,
        string key,
        string querySetName,
        HashSet<string> resourceTypes,
        ValidationResult result)
    {
        var prefix = $"{querySetName}['{key}']";

        // Validate ResourceType
        if (string.IsNullOrWhiteSpace(config.ResourceType))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: ResourceType is required for ParameterQueryConfig.");
        }
        else
        {
            if (config.ResourceType.Length > MaxResourceTypeLength)
            {
                result.IsValid = false;
                result.Errors.Add($"{prefix}: ResourceType length exceeds maximum of {MaxResourceTypeLength} characters.");
            }

            //validate resource string by using the Firely Resource enum
            if (!Enum.TryParse(typeof(ResourceType), config.ResourceType, out _))
            {
                result.IsValid = false;
                result.Errors.Add($"{prefix}: Resource value '{config.ResourceType}' is not a valid FHIR ResourceType.");
            }


            // Track resource types for duplicate detection
            if (resourceTypes.Contains(config.ResourceType))
            {
                result.Warnings.Add($"{prefix}: Duplicate ResourceType '{config.ResourceType}' detected in {querySetName}. This may cause conflicts.");
            }
            else
            {
                resourceTypes.Add(config.ResourceType);
            }
        }

        // Validate OperationType
        if (!Enum.IsDefined(typeof(OperationType), config.OperationType))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: OperationType value '{config.OperationType}' is not a valid OperationType enum value.");
        }
        else if (config.OperationType == OperationType.Read)
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: 'Read' is not a valid operation type for ParameterQueryConfig.");
        }

        // Validate Parameters
        if (config.Parameters == null || !config.Parameters.Any())
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Parameters list cannot be null or empty for ParameterQueryConfig.");
        }
        else
        {
            ValidateParameters(config.Parameters, prefix, result);
        }
    }

    private void ValidateParameters(
        List<IParameter> parameters,
        string prefix,
        ValidationResult result)
    {
        var seenParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int pagedCount = 0;

        for (int i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            var paramPrefix = $"{prefix}.Parameters[{i}]";

            if (parameter == null)
            {
                result.IsValid = false;
                result.Errors.Add($"{paramPrefix}: Parameter cannot be null.");
                continue;
            }

            switch (parameter)
            {
                case LiteralParameter literal:
                    ValidateLiteralParameter(literal, paramPrefix, seenParameterNames, result);
                    break;
                case VariableParameter variable:
                    ValidateVariableParameter(variable, paramPrefix, seenParameterNames, result);
                    break;
                case ResourceIdsParameter resourceIds:
                    ValidateResourceIdsParameter(resourceIds, paramPrefix, seenParameterNames, ref pagedCount, result);
                    break;
                default:
                    result.IsValid = false;
                    result.Errors.Add($"{paramPrefix}: Unknown parameter type '{parameter.GetType().Name}'.");
                    break;
            }
        }

        // Validate that only one paged parameter exists
        if (pagedCount > 1)
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Only one paged parameter is allowed per query configuration. Found {pagedCount}.");
        }
    }

    private void ValidateLiteralParameter(
        LiteralParameter parameter,
        string prefix,
        HashSet<string> seenParameterNames,
        ValidationResult result)
    {
        // Validate Name
        ValidateParameterName(parameter.Name, prefix, seenParameterNames, result);

        // Validate Literal value
        if (string.IsNullOrWhiteSpace(parameter.Literal))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Literal value is required for LiteralParameter.");
        }
    }

    private void ValidateVariableParameter(
        VariableParameter parameter,
        string prefix,
        HashSet<string> seenParameterNames,
        ValidationResult result)
    {
        // Validate Name
        ValidateParameterName(parameter.Name, prefix, seenParameterNames, result);

        // Validate Variable enum is defined
        if (!Enum.IsDefined(typeof(Variable), parameter.Variable))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Variable value '{parameter.Variable}' is not a valid Variable enum value.");
        }

        // Validate Format for date-related variables
        if ((parameter.Variable == Variable.LookbackStart ||
             parameter.Variable == Variable.PeriodStart ||
             parameter.Variable == Variable.PeriodEnd) &&
            string.IsNullOrWhiteSpace(parameter.Format))
        {
            result.Warnings.Add($"{prefix}: Format is recommended for date Variable '{parameter.Variable}' to ensure proper formatting.");
        }
    }

    private void ValidateResourceIdsParameter(
        ResourceIdsParameter parameter,
        string prefix,
        HashSet<string> seenParameterNames,
        ref int pagedCount,
        ValidationResult result)
    {
        // Validate Name
        ValidateParameterName(parameter.Name, prefix, seenParameterNames, result);

        // Validate Resource
        if (string.IsNullOrWhiteSpace(parameter.Resource))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Resource is required for ResourceIdsParameter.");
        }

        //validate resource string by using the Firely Resource enum
        if (!Enum.TryParse(typeof(ResourceType), parameter.Resource, out _))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Resource value '{parameter.Resource}' is not a valid FHIR ResourceType.");
        }

        if (!string.IsNullOrWhiteSpace(parameter.Paged))
        {
            if (!int.TryParse(parameter.Paged, out int pagedVal))
            {
                result.IsValid = false;
                result.Errors.Add($"{prefix}: Paged value '{parameter.Paged}' is not a valid int.");
            }
            else if (pagedVal < 0)
            {
                result.IsValid = false;
                result.Errors.Add($"{prefix}: Paged value cannot be negative.");
            }
            pagedCount++;
        }

    }

    private void ValidateParameterName(
        string name,
        string prefix,
        HashSet<string> seenParameterNames,
        ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Name is required for parameter.");
        }
        else
        {
            if (name.Length > MaxParameterNameLength)
            {
                result.IsValid = false;
                result.Errors.Add($"{prefix}: Name length exceeds maximum of {MaxParameterNameLength} characters.");
            }

            if (seenParameterNames.Contains(name))
            {
                result.Warnings.Add($"{prefix}: Duplicate parameter name '{name}' detected. This may cause unexpected behavior.");
            }
            else
            {
                seenParameterNames.Add(name);
            }
        }
    }

    private void ValidateReferenceQueryConfig(
        ReferenceQueryConfig config,
        string key,
        string querySetName,
        ValidationResult result)
    {
        var prefix = $"{querySetName}['{key}']";

        // Validate ResourceType
        if (string.IsNullOrWhiteSpace(config.ResourceType))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: ResourceType is required for ReferenceQueryConfig.");
        }
        else if (config.ResourceType.Length > MaxResourceTypeLength)
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: ResourceType length exceeds maximum of {MaxResourceTypeLength} characters.");
        }

        //validate resource string by using the Firely Resource enum
        if (!Enum.TryParse(typeof(ResourceType), config.ResourceType, out _))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: ResourceType value '{config.ResourceType}' is not a valid FHIR ResourceType.");
        }

        // Validate OperationType
        if (!Enum.IsDefined(typeof(OperationType), config.OperationType))
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: OperationType value '{config.OperationType}' is not a valid OperationType enum value.");
        }


        // Validate Paged value
        if (config.Paged < 0)
        {
            result.IsValid = false;
            result.Errors.Add($"{prefix}: Paged value cannot be negative.");
        }
        else if (config.Paged == 0)
        {
            result.Warnings.Add($"{prefix}: Paged value is 0. Queries may not be paged as expected.");
        }
    }

    private void ValidateQueryOrder(
        Dictionary<string, IQueryConfig> queries,
        string querySetName,
        ValidationResult result)
    {
        bool seenReference = false;

        var orderedQueries = queries
            .OrderBy(q => int.TryParse(q.Key, out var i) ? i : int.MaxValue)
            .ToList();

        foreach (var kvp in orderedQueries)
        {
            var config = kvp.Value;
            if (config == null) continue;

            if (config is ReferenceQueryConfig)
            {
                seenReference = true;
            }
            else if (config is ParameterQueryConfig && seenReference)
            {
                result.IsValid = false;
                result.Errors.Add($"{querySetName}['{kvp.Key}']: All ParameterQueryConfig entries must appear before all ReferenceQueryConfig entries.");
            }
        }
    }

    private void ValidateCrossReferences(
        Dictionary<string, IQueryConfig> initialQueries,
        Dictionary<string, IQueryConfig> supplementalQueries,
        ValidationResult result)
    {
        // Collect all resource types that can be referenced
        var availableResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add resource types from InitialQueries
        foreach (var config in initialQueries.Values)
        {
            if (config is ParameterQueryConfig paramConfig && !string.IsNullOrWhiteSpace(paramConfig.ResourceType))
            {
                availableResources.Add(paramConfig.ResourceType);
            }
            else if (config is ReferenceQueryConfig refConfig && !string.IsNullOrWhiteSpace(refConfig.ResourceType))
            {
                availableResources.Add(refConfig.ResourceType);
            }
        }

        // Add resource types from SupplementalQueries
        foreach (var config in supplementalQueries.Values)
        {
            if (config is ParameterQueryConfig paramConfig && !string.IsNullOrWhiteSpace(paramConfig.ResourceType))
            {
                availableResources.Add(paramConfig.ResourceType);
            }
            else if (config is ReferenceQueryConfig refConfig && !string.IsNullOrWhiteSpace(refConfig.ResourceType))
            {
                availableResources.Add(refConfig.ResourceType);
            }
        }

        // Validate ResourceIdsParameter references in InitialQueries
        ValidateResourceReferences(initialQueries, "InitialQueries", availableResources, result);

        // Validate ResourceIdsParameter references in SupplementalQueries
        ValidateResourceReferences(supplementalQueries, "SupplementalQueries", availableResources, result);
    }

    private void ValidateResourceReferences(
        Dictionary<string, IQueryConfig> queries,
        string querySetName,
        HashSet<string> availableResources,
        ValidationResult result)
    {
        foreach (var kvp in queries)
        {
            var key = kvp.Key;
            var config = kvp.Value;

            if (config is ParameterQueryConfig paramConfig && paramConfig.Parameters != null)
            {
                foreach (var parameter in paramConfig.Parameters)
                {
                    if (parameter is ResourceIdsParameter resourceIds &&
                        !string.IsNullOrWhiteSpace(resourceIds.Resource))
                    {
                        if (!availableResources.Contains(resourceIds.Resource))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"{querySetName}['{key}']: ResourceIdsParameter references unknown Resource '{resourceIds.Resource}'. " +
                                            $"Ensure a query configuration for this resource type exists.");
                        }
                    }
                }
            }
        }
    }
}