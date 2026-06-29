using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public abstract class BaseOperationService<TOperation>
        where TOperation : class
    {
        private readonly TimeSpan _operationTimeout;
        protected readonly ILogger Logger;

        protected BaseOperationService(ILogger logger, TimeSpan? operationTimeout = null)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(120);
        }

        public async Task<OperationResult> ProcessOperationAsync(TOperation operation, DomainResource resource, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                return OperationResult.Failure("Operation cannot be null.");

            if (resource == null)
                return OperationResult.Failure("Resource cannot be null.");

            var result = await ProcessOperation(operation, resource, cancellationToken);

            return result;
        }

        protected virtual async Task<OperationResult> ProcessOperation(TOperation operation, DomainResource resource, CancellationToken cancellationToken = default)
        {
            //Daniel - 4/2026: Do we need to perform a deep copy? Commented this out for now as it looks like the operations are being applied without it. 
            //var resourceCopy = resource.DeepCopy() as DomainResource;
            //if (resourceCopy == null)
            //    return OperationResult.Failure($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");

            try
            {
                //return await ExecuteOperation(operation, resourceCopy);
                return await ExecuteOperation(operation, resource, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process {OperationType} operation for resource type {ResourceType}.", typeof(TOperation).Name, resource.TypeName);
                //return OperationResult.Failure($"Failed to process {typeof(TOperation).Name} operation: {ex.Message}", resourceCopy);
                return OperationResult.Failure($"Failed to process {typeof(TOperation).Name} operation: {ex.Message}", resource);
            }
        }

        protected abstract Task<OperationResult> ExecuteOperation(TOperation operation, DomainResource resource, CancellationToken cancellationToken = default);
    }
}