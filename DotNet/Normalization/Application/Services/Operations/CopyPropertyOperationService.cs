using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation;
using System.Collections;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CopyPropertyOperationService : BaseOperationService<CopyPropertyOperation>
    {
        public CopyPropertyOperationService(ILogger<CopyPropertyOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
        }

        protected override async Task<OperationResult> ExecuteOperation(CopyPropertyOperation operation, DomainResource resource)
        {
            return await CopyFhirPathValue(resource, operation.SourceFhirPath, operation.TargetFhirPath);
        }

        private async Task<OperationResult> CopyFhirPathValue(DomainResource resource, string sourceFhirPath, string targetFhirPath)
        {
            var result = await FhirPathValidator.IsFhirPathValidForResourceType(sourceFhirPath, resource.TypeName);
            if (!result.IsValid)
                return OperationResult.Failure($"Invalid target FHIRPath expression: {sourceFhirPath}. {result.ErrorMessage}", resource);

            result = await FhirPathValidator.IsFhirPathValidForResourceType(targetFhirPath, resource.TypeName);
            if (!result.IsValid)
                return OperationResult.Failure($"Invalid target FHIRPath expression: {targetFhirPath}. {result.ErrorMessage}", resource);

            var scopedNode = resource.ToTypedElement();
            var sourceValueResult = OperationServiceHelper.ExtractValueFromFhirPath(scopedNode, sourceFhirPath, Logger);
            object sourceValue = sourceValueResult.Success
                ? sourceValueResult.Value
                : OperationServiceHelper.GetValueReflectively(resource, sourceFhirPath);

            if (sourceValue == null)
                return OperationResult.Failure($"No values found at source FHIRPath: {sourceFhirPath} for resource type {resource.TypeName}.", resource);

            if (sourceValue is string or int or bool or decimal or DateTime ||
                sourceValue is IList valueList && valueList.Cast<object>().All(v => v is string or int or bool or decimal or DateTime))
            {
                return SetValue(resource, targetFhirPath, sourceValue, scopedNode, Logger);
            }
            else if (sourceValue is Base complexValue)
            {
                var validationResult = ValidateComplexTypeCompatibility(scopedNode, targetFhirPath, complexValue);
                if (!validationResult.Result)
                    return OperationResult.Failure(validationResult.ErrorMessage, resource);

                return SetValue(resource, targetFhirPath, complexValue, scopedNode, Logger);
            }

            return OperationResult.Failure($"Source type {sourceValue.GetType().Name} is not supported at source FHIRPath: {sourceFhirPath}.", resource);
        }

        public static OperationResult SetValue(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode, ILogger? logger = null)
        {
            var pathParts = targetFhirPath.Split('.');
            var parentPath = pathParts.Length > 1 ? string.Join(".", pathParts.Take(pathParts.Length - 1)) : string.Empty;

            // Ensure parent structure exists
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parentPoco = OperationServiceHelper.CreateParentStructure(resource, parentPath, logger);
                if (parentPoco == null)
                    return OperationResult.Failure($"Could not create parent structure for {parentPath} in resource type {resource.TypeName}.", resource);
            }

            // Try setting the value using FHIRPath
            var setResult = OperationServiceHelper.SetValueViaFhirPath(resource, targetFhirPath, targetValue, scopedNode, logger);
            if (setResult.Result)
                return OperationResult.Success(resource);

            // If FHIRPath fails, try reflective setting
            setResult = OperationServiceHelper.ResolveAndSetValueReflectively(resource, targetFhirPath, targetValue, logger);
            if (setResult.Result)
                return OperationResult.Success(resource);

            // If reflective setting fails, try creating and setting the target element
            setResult = OperationServiceHelper.CreateAndSetTargetElement(resource, targetFhirPath, targetValue, logger);
            return setResult.Result
                ? OperationResult.Success(resource)
                : OperationResult.Failure(setResult.ErrorMessage, resource);
        }

        private OperationServiceHelper.SetValueResult ValidateComplexTypeCompatibility(ITypedElement scopedNode, string targetFhirPath, Base copiedObject)
        {
            try
            {
                var pathParts = targetFhirPath.Split('.');
                if (pathParts.Length < 2)
                    return OperationServiceHelper.SetValueResult.Success();

                var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                var parentNode = scopedNode.Select(parentPath).FirstOrDefault();
                if (parentNode == null)
                    return OperationServiceHelper.SetValueResult.Success();

                var parentPoco = parentNode.ToPoco() as Base;
                if (parentPoco == null)
                    return OperationServiceHelper.SetValueResult.Success();

                var propertyName = pathParts.Last().Split('[')[0];
                propertyName = OperationServiceHelper.MapFhirPathToPropertyName(propertyName, parentPoco.GetType());
                var property = OperationServiceHelper.GetProperty(parentPoco.GetType(), propertyName);
                if (property != null && !property.PropertyType.IsAssignableFrom(copiedObject.GetType()) &&
                    !(typeof(IList).IsAssignableFrom(property.PropertyType) &&
                      property.PropertyType.GenericTypeArguments.Length > 0 &&
                      property.PropertyType.GenericTypeArguments[0].IsAssignableFrom(copiedObject.GetType())))
                {
                    return OperationServiceHelper.SetValueResult.Failure(
                        $"Target property {propertyName} of type {property.PropertyType.Name} cannot accept source object of type {copiedObject.GetType().Name} for FHIRPath {targetFhirPath}.");
                }

                return OperationServiceHelper.SetValueResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to validate complex type compatibility for FHIRPath '{TargetFhirPath}' for resource type {ResourceType}.", targetFhirPath, scopedNode.Name);
                return OperationServiceHelper.SetValueResult.Failure($"Failed to validate complex type compatibility for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }
    }
}