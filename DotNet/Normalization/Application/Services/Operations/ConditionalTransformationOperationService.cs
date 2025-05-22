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
    /// <summary>
    /// A background service that executes conditional transform operations on FHIR resources asynchronously via a queue.
    /// </summary>
    public class ConditionalTransformOperationService : BackgroundService
    {
        private readonly ConcurrentQueue<(ConditionalTransformOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)> _operationQueue = new();
        private readonly TimeSpan _operationTimeout;
        private readonly ILogger<ConditionalTransformOperationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionalTransformOperationService"/> class.
        /// </summary>
        public ConditionalTransformOperationService(ILogger<ConditionalTransformOperationService> logger, TimeSpan? operationTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(120);
        }

        /// <summary>
        /// Enqueues a conditional transform operation for asynchronous execution and returns a task to await the result.
        /// </summary>
        public async Task<OperationResult> EnqueueOperationAsync(ConditionalTransformOperation operation, DomainResource resource)
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
                _logger.LogError(tex, "Conditional transform operation timed out after {Timeout}.", _operationTimeout);
                return OperationResult.Failure($"Conditional transform operation timed out after {_operationTimeout}.");
            }
        }

        /// <summary>
        /// Executes the background service, processing queued conditional transform operations in batches.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<(ConditionalTransformOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)>();
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
                        _logger.LogError("Failed conditional transform operation: {ErrorMessage}", result.ErrorMessage);
                    }
                }

                if (batch.Count == 0)
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Processes a single conditional transform operation.
        /// </summary>
        private OperationResult ProcessOperation(ConditionalTransformOperation operation, DomainResource resource)
        {
            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
            {
                return OperationResult.Failure($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");
            }

            return ExecuteConditionalTransform(resourceCopy, operation);
        }

        /// <summary>
        /// Executes the conditional transform logic, checking conditions and applying the transformation if all conditions pass.
        /// </summary>
        private OperationResult ExecuteConditionalTransform(DomainResource resource, ConditionalTransformOperation operation)
        {
            foreach (var condition in operation.Conditions)
            {
                if (!condition.Is_Passed(resource))
                {
                    return OperationResult.Success(resource);
                }
            }

            return SetTransformValue(resource, operation.TargetFhirPath, operation.TargetValue);
        }

        /// <summary>
        /// Sets the target value at the specified FHIRPath.
        /// </summary>
        private OperationResult SetTransformValue(DomainResource resource, string targetFhirPath, object targetValue)
        {
            if (!OperationServiceHelper.ValidateFhirPath(targetFhirPath, out var targetValidationError, _logger))
            {
                return OperationResult.Failure($"Invalid target FHIRPath expression: {targetFhirPath}. {targetValidationError}", resource);
            }

            var scopedNode = resource.ToTypedElement();

            var setResult = SetValueViaFhirPath(resource, targetFhirPath, targetValue, scopedNode);
            if (setResult.Result)
            {
                return OperationResult.Success(resource);
            }

            var reflectiveSetResult = ResolveAndSetValueReflectively(resource, targetFhirPath, targetValue);
            if (reflectiveSetResult.Result)
            {
                return OperationResult.Success(resource);
            }

            var createSetResult = CreateAndSetTargetElement(resource, targetFhirPath, targetValue);
            return createSetResult.Result
                ? OperationResult.Success(resource)
                : OperationResult.Failure(createSetResult.ErrorMessage, resource);
        }

        /// <summary>
        /// Result of setting a value via FHIRPath or reflection.
        /// </summary>
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

        /// <summary>
        /// Sets a value at a target FHIRPath using FHIRPath evaluation.
        /// </summary>
        private SetValueResult SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode)
        {
            try
            {
                var targetElements = scopedNode.Select(targetFhirPath).ToList();
                if (!targetElements.Any())
                {
                    return SetValueResult.Failure($"No target elements found for FHIRPath {targetFhirPath}.");
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

                    var convertedValue = OperationServiceHelper.ConvertToFhirType(targetValue, propertyToSet.PropertyType, parentPoco, propertyToSet.Name, _logger);
                    propertyToSet.SetValue(parentPoco, convertedValue);
                }
                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate target FHIRPath '{TargetFhirPath}' for resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                return SetValueResult.Failure($"Failed to evaluate target FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a value at a target FHIRPath using reflection.
        /// </summary>
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

                    var convertedValue = OperationServiceHelper.ConvertToFhirType(targetValue, property.PropertyType, resource, property.Name, _logger);
                    property.SetValue(resource, convertedValue);
                }
                else
                {

                    var (parentPoco, property) = OperationServiceHelper.NavigateFhirPath(resource, targetFhirPath, createIfMissing: true, _logger);
                    if (property == null || !property.CanWrite)
                    {
                        return SetValueResult.Failure($"Property not found or not writable for FHIRPath {targetFhirPath}.");
                    }

                    var convertedValue = OperationServiceHelper.ConvertToFhirType(targetValue, property.PropertyType, parentPoco, property.Name, _logger);
                    property.SetValue(parentPoco, convertedValue);
                }

                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve and set value reflectively for FHIRPath '{TargetFhirPath}'.", targetFhirPath);
                return SetValueResult.Failure($"Failed to set value reflectively for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a parent structure and sets a value at a target FHIRPath.
        /// </summary>
        private SetValueResult CreateAndSetTargetElement(Resource resource, string targetFhirPath, object newValue)
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
                    var convertedValue = OperationServiceHelper.ConvertToFhirType(newValue, property.PropertyType, resource, propertyName, _logger);
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
                            var convertedItem = OperationServiceHelper.ConvertToFhirType(item, property.PropertyType.GenericTypeArguments[0], parentPoco, propertyName, _logger);
                            list.Add(convertedItem);
                        }
                        property.SetValue(parentPoco, list);
                    }
                    else
                    {
                        var convertedValue = OperationServiceHelper.ConvertToFhirType(newValue, property.PropertyType, parentPoco, propertyName, _logger);
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
    }
}