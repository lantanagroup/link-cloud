using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using System.Collections;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CopyPropertyOperationService : BackgroundService
    {
        private readonly ConcurrentQueue<(CopyPropertyOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)> _operationQueue = new();
        private readonly TimeSpan _operationTimeout;
        private readonly ILogger<CopyPropertyOperationService> _logger;

        public CopyPropertyOperationService(ILogger<CopyPropertyOperationService> logger, TimeSpan? operationTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(120);
        }

        public async Task<OperationResult> EnqueueOperationAsync(CopyPropertyOperation operation, DomainResource resource)
        {
            if (operation == null)
            {
                return OperationResult.Failure("Operation cannot be null.");
            }

            if (resource == null)
            {
                return OperationResult.Failure("Resource cannot be null.");
            }

            var tcs = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _operationQueue.Enqueue((operation, resource, tcs));

            try
            {
                return await tcs.Task.WaitAsync(_operationTimeout, CancellationToken.None);
            }
            catch (TimeoutException tex)
            {
                _logger.LogError(tex, "Copy operation '{OperationName}' timed out after {Timeout}.", operation.Name, _operationTimeout);
                return OperationResult.Failure($"Copy operation '{operation.Name}' timed out after {_operationTimeout}.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<(CopyPropertyOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)>();
                while (_operationQueue.TryDequeue(out var item) && batch.Count < 10)
                {
                    batch.Add(item);
                }

                foreach (var item in batch)
                {
                    var result = ProcessOperation(item.Operation, item.Resource);
                    item.Result.SetResult(result);
                    if (result.SuccessCode != OperationStatus.Success)
                    {
                        _logger.LogError("Failed operation {OperationName}: {ErrorMessage}", item.Operation.Name, result.ErrorMessage);
                    }
                }

                if (batch.Count == 0)
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
        }

        private OperationResult ProcessOperation(CopyPropertyOperation operation, DomainResource resource)
        {
            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
            {
                return OperationResult.Failure($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");
            }

            return CopyFhirPathValue(resourceCopy, operation.SourceFhirPath, operation.TargetFhirPath, resource);
        }

        private OperationResult CopyFhirPathValue(DomainResource resource, string sourceFhirPath, string targetFhirPath, DomainResource originalResource)
        {
            if (!OperationServiceHelper.ValidateFhirPath(sourceFhirPath, out var sourceValidationError, _logger))
            {
                return OperationResult.Failure($"Invalid source FHIRPath expression: {sourceFhirPath}. {sourceValidationError}", resource);
            }

            if (!OperationServiceHelper.ValidateFhirPath(targetFhirPath, out var targetValidationError, _logger))
            {
                return OperationResult.Failure($"Invalid target FHIRPath expression: {targetFhirPath}. {targetValidationError}", resource);
            }

            var scopedNode = resource.ToTypedElement();
            var sourceValueResult = OperationServiceHelper.ExtractValueFromFhirPath(scopedNode, sourceFhirPath, _logger);
            object sourceValue;

            if (sourceValueResult.Success)
            {
                sourceValue = sourceValueResult.Value;
            }
            else
            {
                var reflectiveValue = OperationServiceHelper.GetValueReflectively(resource, sourceFhirPath);
                if (reflectiveValue == null)
                {
                    return OperationResult.Failure($"No values found at source FHIRPath: {sourceFhirPath} for resource type {resource.TypeName}.", resource);
                }
                sourceValue = reflectiveValue;
            }

            if (sourceValue is string || sourceValue is int || sourceValue is bool || sourceValue is decimal || sourceValue is DateTime
                || sourceValue is IList valueList && valueList.Cast<object>().All(v => v is string || v is int || v is bool || v is decimal || v is DateTime))
            {
                var setResult = SetValueViaFhirPath(resource, targetFhirPath, sourceValue, scopedNode, sourceFhirPath, originalResource);
                if (setResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var reflectiveSetResult = ResolveAndSetValueReflectively(resource, targetFhirPath, sourceValue);
                if (reflectiveSetResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var createSetResult = CreateAndSetTargetElement(resource, targetFhirPath, sourceValue, sourceFhirPath, originalResource);
                return createSetResult.Result
                    ? OperationResult.Success(resource)
                    : OperationResult.Failure(createSetResult.ErrorMessage, resource);
            }
            else if (sourceValue is Base complexValue)
            {
                var validationResult = ValidateComplexTypeCompatibility(scopedNode, targetFhirPath, complexValue);
                if (!validationResult.Result)
                {
                    return OperationResult.Failure(validationResult.ErrorMessage, resource);
                }

                var setResult = SetValueViaFhirPath(resource, targetFhirPath, complexValue, scopedNode, sourceFhirPath, originalResource);
                if (setResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var reflectiveSetResult = ResolveAndSetValueReflectively(resource, targetFhirPath, complexValue);
                if (reflectiveSetResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var createSetResult = CreateAndSetTargetElement(resource, targetFhirPath, complexValue, sourceFhirPath, originalResource);
                return createSetResult.Result
                    ? OperationResult.Success(resource)
                    : OperationResult.Failure(createSetResult.ErrorMessage, resource);
            }
            else
            {
                return OperationResult.Failure($"Source type {sourceValue.GetType().Name} is not supported at source FHIRPath: {sourceFhirPath}.", resource);
            }
        }

        private class SetValueResult
        {
            public bool Result { get; }
            public string ErrorMessage { get; }

            public SetValueResult(bool success, string errorMessage)
            {
                Result = success;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public static SetValueResult Success() => new SetValueResult(true, string.Empty);
            public static SetValueResult Failure(string errorMessage) => new SetValueResult(false, errorMessage);
        }

        private SetValueResult SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode, string sourceFhirPath, DomainResource originalResource)
        {
            try
            {
                var targetElements = scopedNode.Select(targetFhirPath).ToList();
                if (!targetElements.Any())
                {
                    return SetValueResult.Failure("No target elements found.");
                }

                foreach (var targetElement in targetElements)
                {
                    if (string.IsNullOrEmpty(targetElement.Location))
                    {
                        return SetValueResult.Failure($"Target element at FHIRPath {targetFhirPath} has no location.");
                    }

                    var targetPath = targetElement.Location;
                    var pathParts = targetPath.Split('.').Skip(1).ToArray();
                    var (parentPoco, propertyToSet) = pathParts.Length == 0
                        ? (resource, OperationServiceHelper.GetProperty(resource.GetType(), targetFhirPath))
                        : OperationServiceHelper.NavigateFhirPath(resource, string.Join(".", pathParts), createIfMissing: true, _logger);

                    if (parentPoco == null || propertyToSet == null)
                    {
                        return SetValueResult.Failure($"Could not resolve parent or property for target path {targetPath} in resource type {resource.TypeName}.");
                    }

                    if (!propertyToSet.CanWrite)
                    {
                        return SetValueResult.Failure($"Property {propertyToSet.Name} on type {parentPoco.GetType().Name} is not writable for FHIRPath {targetFhirPath}.");
                    }

                    if (targetValue is IList valueList && typeof(IList).IsAssignableFrom(propertyToSet.PropertyType))
                    {
                        var list = (IList)Activator.CreateInstance(propertyToSet.PropertyType);
                        foreach (var item in valueList)
                        {
                            var convertedItem = OperationServiceHelper.ConvertToFhirType(item, propertyToSet.PropertyType.GenericTypeArguments[0], propertyToSet.Name, _logger);
                            list.Add(convertedItem);
                        }
                        propertyToSet.SetValue(parentPoco, list);
                    }
                    else
                    {
                        var convertedValue = OperationServiceHelper.ConvertToFhirType(targetValue, propertyToSet.PropertyType, propertyToSet.Name, _logger);
                        propertyToSet.SetValue(parentPoco, convertedValue);
                    }
                }
                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate target FHIRPath '{TargetFhirPath}' for resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                return SetValueResult.Failure($"Failed to evaluate target FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        private SetValueResult ResolveAndSetValueReflectively(DomainResource resource, string targetFhirPath, object targetValue)
        {
            var pathParts = targetFhirPath.Split('.');
            try
            {
                if (pathParts.Length == 1)
                {
                    var property = OperationServiceHelper.GetProperty(resource.GetType(), pathParts[0]);
                    if (property == null || !property.CanWrite)
                    {
                        return SetValueResult.Failure($"Property {pathParts[0]} not found or not writable for FHIRPath {targetFhirPath}.");
                    }

                    var convertedValue = OperationServiceHelper.ConvertToFhirType(targetValue, property.PropertyType, property.Name, _logger);
                    property.SetValue(resource, convertedValue);
                }
                else
                {
                    var (parentPoco, property) = OperationServiceHelper.NavigateFhirPath(resource, targetFhirPath, createIfMissing: true, _logger);
                    if (property == null || !property.CanWrite)
                    {
                        return SetValueResult.Failure($"Property not found or not writable for FHIRPath {targetFhirPath}.");
                    }

                    if (targetValue is IList valueList && typeof(IList).IsAssignableFrom(property.PropertyType))
                    {
                        var vList = (IList)Activator.CreateInstance(property.PropertyType);
                        foreach (var item in valueList)
                        {
                            var convertedItem = OperationServiceHelper.ConvertToFhirType(item, property.PropertyType.GenericTypeArguments[0], property.Name, _logger);
                            vList.Add(convertedItem);
                        }
                        property.SetValue(parentPoco, vList);
                    }
                    else
                    {
                        var convertedValue = OperationServiceHelper.ConvertToFhirType(targetValue, property.PropertyType, property.Name, _logger);
                        if (pathParts.Last().Contains("[") && typeof(IList).IsAssignableFrom(property.PropertyType))
                        {
                            var (_, arrayIndex) = OperationServiceHelper.ParseFhirPathPart(pathParts.Last());
                            var vList = property.GetValue(parentPoco) as IList;
                            if (vList == null)
                            {
                                vList = (IList)Activator.CreateInstance(property.PropertyType);
                                property.SetValue(parentPoco, vList);
                            }

                            while (vList.Count <= arrayIndex.Value)
                            {
                                var itemType = property.PropertyType.GenericTypeArguments[0];
                                var newItem = Activator.CreateInstance(itemType);
                                vList.Add(newItem);
                            }
                            vList[arrayIndex.Value] = convertedValue;
                        }
                        else
                        {
                            property.SetValue(parentPoco, convertedValue);
                        }
                    }
                }

                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve and set value reflectively for FHIRPath '{TargetFhirPath}'.", targetFhirPath);
                return SetValueResult.Failure($"Failed to set value reflectively for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        private SetValueResult CreateAndSetTargetElement(Resource resource, string targetFhirPath, object newValue, string sourceFhirPath, DomainResource originalResource)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length == 1)
            {
                var propertyName = pathParts[0];
                var property = OperationServiceHelper.GetProperty(resource.GetType(), propertyName);
                if (property == null)
                {
                    return SetValueResult.Failure($"Property {propertyName} not found on type {resource.TypeName} for FHIRPath {targetFhirPath}.");
                }

                try
                {
                    var convertedValue = OperationServiceHelper.ConvertToFhirType(newValue, property.PropertyType, propertyName, _logger);
                    if (!property.CanWrite)
                    {
                        return SetValueResult.Failure($"Property {propertyName} on type {resource.TypeName} is not writable for FHIRPath {targetFhirPath}.");
                    }
                    property.SetValue(resource, convertedValue);
                    return SetValueResult.Success();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set target element for FHIRPath '{TargetFhirPath}' in resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                    return SetValueResult.Failure($"Failed to set target element for FHIRPath '{targetFhirPath}': {ex.Message}");
                }
            }
            else
            {

                var propertyName = pathParts.Last();
                int? arrayIndex = null;
                if (propertyName.Contains("[") && propertyName.EndsWith("]"))
                {
                    (propertyName, arrayIndex) = OperationServiceHelper.ParseFhirPathPart(propertyName);
                }

                var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                var parentPoco = OperationServiceHelper.CreateParentStructure(resource, parentPath, _logger);
                if (parentPoco == null)
                {
                    return SetValueResult.Failure($"Could not create parent structure for {parentPath} in resource type {resource.TypeName}.");
                }

                propertyName = OperationServiceHelper.MapFhirPathToPropertyName(propertyName, parentPoco.GetType());
                var property = OperationServiceHelper.GetProperty(parentPoco.GetType(), propertyName);
                if (property == null)
                {
                    return SetValueResult.Failure($"Property {propertyName} not found on parent type {parentPoco.GetType().Name} for FHIRPath {targetFhirPath}.");
                }

                try
                {
                    if (newValue is IList valueList && typeof(IList).IsAssignableFrom(property.PropertyType))
                    {
                        var list = (IList)Activator.CreateInstance(property.PropertyType);
                        foreach (var item in valueList)
                        {
                            var convertedItem = OperationServiceHelper.ConvertToFhirType(item, property.PropertyType.GenericTypeArguments[0], propertyName, _logger);
                            list.Add(convertedItem);
                        }
                        property.SetValue(parentPoco, list);
                    }
                    else
                    {
                        var convertedValue = OperationServiceHelper.ConvertToFhirType(newValue, property.PropertyType, propertyName, _logger);
                        if (typeof(IList).IsAssignableFrom(property.PropertyType))
                        {
                            var list = property.GetValue(parentPoco) as IList;
                            if (list == null)
                            {
                                list = (IList)Activator.CreateInstance(property.PropertyType);
                                property.SetValue(parentPoco, list);
                            }

                            if (arrayIndex.HasValue)
                            {
                                while (list.Count <= arrayIndex.Value)
                                {
                                    var itemType = property.PropertyType.GenericTypeArguments[0];
                                    var newItem = Activator.CreateInstance(itemType);
                                    list.Add(newItem);
                                }
                                list[arrayIndex.Value] = convertedValue;
                            }
                            else
                            {
                                if (list.Count == 0)
                                {
                                    list.Add(convertedValue);
                                }
                                else
                                {
                                    list[0] = convertedValue;
                                }
                            }
                        }
                        else
                        {
                            property.SetValue(parentPoco, convertedValue);
                        }
                    }

                    return SetValueResult.Success();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set target element for FHIRPath '{TargetFhirPath}' in resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                    return SetValueResult.Failure($"Failed to set target element for FHIRPath '{targetFhirPath}': {ex.Message}");
                }
            }
        }

        private SetValueResult ValidateComplexTypeCompatibility(ITypedElement scopedNode, string targetFhirPath, Base copiedObject)
        {
            try
            {
                var pathParts = targetFhirPath.Split('.');
                if (pathParts.Length < 2)
                {
                    return SetValueResult.Success();
                }

                var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                var parentNodes = scopedNode.Select(parentPath).ToList();
                var parentNode = parentNodes.FirstOrDefault();
                if (parentNode == null)
                {
                    return SetValueResult.Success();
                }

                var parentPoco = parentNode.ToPoco() as Base;
                if (parentPoco == null)
                {
                    return SetValueResult.Success();
                }

                var propertyName = pathParts.Last().Split('[')[0];
                propertyName = OperationServiceHelper.MapFhirPathToPropertyName(propertyName, parentPoco.GetType());
                var property = OperationServiceHelper.GetProperty(parentPoco.GetType(), propertyName);
                if (property != null && !property.PropertyType.IsAssignableFrom(copiedObject.GetType()) &&
                    !(typeof(IList).IsAssignableFrom(property.PropertyType) &&
                      property.PropertyType.GenericTypeArguments.Length > 0 &&
                      property.PropertyType.GenericTypeArguments[0].IsAssignableFrom(copiedObject.GetType())))
                {
                    return SetValueResult.Failure(
                        $"Target property {propertyName} of type {property.PropertyType.Name} cannot accept source object of type {copiedObject.GetType().Name} for FHIRPath {targetFhirPath}.");
                }

                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate complex type compatibility for FHIRPath '{TargetFhirPath}' for resource type {ResourceType}.", targetFhirPath, scopedNode.Name);
                return SetValueResult.Failure($"Failed to validate complex type compatibility for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }
    }
}