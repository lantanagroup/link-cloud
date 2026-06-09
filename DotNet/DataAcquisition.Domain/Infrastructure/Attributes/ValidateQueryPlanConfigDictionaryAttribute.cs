using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Attributes;

/// <summary>
/// Validates that a dictionary of IQueryConfig contains valid entries with proper structure
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ValidateQueryPlanConfigDictionaryAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return new ValidationResult("Query configuration dictionary cannot be null.");
        }

        if (value is not Dictionary<string, IQueryConfig> dictionary)
        {
            return new ValidationResult("Value must be a Dictionary<string, IQueryConfig>.");
        }

        if (dictionary.Count == 0)
        {
            return new ValidationResult($"{validationContext.DisplayName} must contain at least one query configuration.");
        }

        var errors = new List<string>();

        // Validate each entry in the dictionary
        foreach (var kvp in dictionary)
        {
            var key = kvp.Key;
            var config = kvp.Value;

            // Validate key
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("Dictionary keys cannot be null or whitespace.");
                continue;
            }

            // Validate config is not null
            if (config == null)
            {
                errors.Add($"Query configuration at key '{key}' cannot be null.");
                continue;
            }

            // Validate based on config type
            switch (config)
            {
                case ParameterQueryConfig paramConfig:
                    ValidateParameterQueryConfig(paramConfig, key, errors);
                    break;
                case ReferenceQueryConfig refConfig:
                    ValidateReferenceQueryConfig(refConfig, key, errors);
                    break;
                default:
                    errors.Add($"Unknown query configuration type at key '{key}': {config.GetType().Name}");
                    break;
            }
        }

        if (errors.Any())
        {
            var errorMessage = $"{validationContext.DisplayName} validation failed:{Environment.NewLine}" +
                              string.Join(Environment.NewLine, errors.Select(e => $"  - {e}"));
            return new ValidationResult(errorMessage);
        }

        return ValidationResult.Success;
    }

    private void ValidateParameterQueryConfig(ParameterQueryConfig config, string key, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(config.ResourceType))
        {
            errors.Add($"Key '{key}': ResourceType is required for ParameterQueryConfig.");
        }

        if (config.Parameters == null || config.Parameters.Count == 0)
        {
            errors.Add($"Key '{key}': Parameters list cannot be null or empty for ParameterQueryConfig.");
            return;
        }

        if (!Enum.IsDefined(typeof(OperationType), config.OperationType))
        {
            errors.Add($"Key '{key}': Invalid OperationType value for ParameterQueryConfig.");
        }
        else if (config.OperationType == OperationType.Read)
        {
            errors.Add($"Key '{key}': 'Read' is not a valid operation type for ParameterQueryConfig.");
        }

        // Validate each parameter
        for (int i = 0; i < config.Parameters.Count; i++)
        {
            var parameter = config.Parameters[i];
            if (parameter == null)
            {
                errors.Add($"Key '{key}': Parameter at index {i} cannot be null.");
                continue;
            }

            switch (parameter)
            {
                case LiteralParameter literal:
                    if (string.IsNullOrWhiteSpace(literal.Name))
                        errors.Add($"Key '{key}': LiteralParameter at index {i} must have a Name.");
                    if (string.IsNullOrWhiteSpace(literal.Literal))
                        errors.Add($"Key '{key}': LiteralParameter at index {i} must have a Literal value.");
                    break;

                case VariableParameter variable:
                    if (string.IsNullOrWhiteSpace(variable.Name))
                        errors.Add($"Key '{key}': VariableParameter at index {i} must have a Name.");
                    if (!Enum.IsDefined(typeof(Variable), variable.Variable))
                        errors.Add($"Key '{key}': VariableParameter at index {i} has invalid Variable value.");
                    break;

                case ResourceIdsParameter resourceIds:
                    if (string.IsNullOrWhiteSpace(resourceIds.Name))
                        errors.Add($"Key '{key}': ResourceIdsParameter at index {i} must have a Name.");
                    if (string.IsNullOrWhiteSpace(resourceIds.Resource))
                        errors.Add($"Key '{key}': ResourceIdsParameter at index {i} must have a Resource.");
                    if (string.IsNullOrWhiteSpace(resourceIds.Paged))
                        errors.Add($"Key '{key}': ResourceIdsParameter at index {i} must have a Paged value.");
                    else if (!int.TryParse(resourceIds.Paged, out _))
                        errors.Add($"Key '{key}': ResourceIdsParameter at index {i} has invalid Paged value (must be a valid integer).");
                    break;
            }
        }
    }

    private void ValidateReferenceQueryConfig(ReferenceQueryConfig config, string key, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(config.ResourceType))
        {
            errors.Add($"Key '{key}': ResourceType is required for ReferenceQueryConfig.");
        }

        if (!Enum.IsDefined(typeof(OperationType), config.OperationType))
        {
            errors.Add($"Key '{key}': Invalid OperationType value for ReferenceQueryConfig.");
        }

        if (config.Paged < 0)
        {
            errors.Add($"Key '{key}': Paged value cannot be negative for ReferenceQueryConfig.");
        }
    }
}