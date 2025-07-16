using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public abstract class BaseOperationService<TOperation> : BackgroundService
        where TOperation : class
    {
        private readonly ConcurrentQueue<(TOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)> _operationQueue = new();
        private readonly TimeSpan _operationTimeout;
        protected readonly ILogger Logger;

        protected BaseOperationService(ILogger logger, TimeSpan? operationTimeout = null)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(120);
        }

        public async Task<OperationResult> EnqueueOperationAsync(TOperation operation, DomainResource resource)
        {
            if (operation == null)
                return OperationResult.Failure("Operation cannot be null.");

            if (resource == null)
                return OperationResult.Failure("Resource cannot be null.");

            var tcs = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _operationQueue.Enqueue((operation, resource, tcs));

            try
            {
                return await tcs.Task.WaitAsync(_operationTimeout, CancellationToken.None);
            }
            catch (TimeoutException tex)
            {
                Logger.LogError(tex, "{OperationType} operation timed out after {Timeout}.", typeof(TOperation).Name, _operationTimeout);
                return OperationResult.Failure($"{typeof(TOperation).Name} operation timed out after {_operationTimeout}.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<(TOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)>();
                while (_operationQueue.TryDequeue(out var item) && batch.Count < 10)
                    batch.Add(item);

                foreach (var item in batch)
                {
                    var result = await ProcessOperation(item.Operation, item.Resource);
                    item.Result.SetResult(result);
                    if (result.SuccessCode != OperationStatus.Success)
                        Logger.LogError("Failed {OperationType} operation: {ErrorMessage}", typeof(TOperation).Name, result.ErrorMessage);
                }

                if (batch.Count == 0)
                    await Task.Delay(100, stoppingToken);
            }
        }

        protected virtual async Task<OperationResult> ProcessOperation(TOperation operation, DomainResource resource)
        {
            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
                return OperationResult.Failure($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");

            try
            {
                return await ExecuteOperation(operation, resourceCopy);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process {OperationType} operation for resource type {ResourceType}.", typeof(TOperation).Name, resource.TypeName);
                return OperationResult.Failure($"Failed to process {typeof(TOperation).Name} operation: {ex.Message}", resourceCopy);
            }
        }

        protected abstract Task<OperationResult> ExecuteOperation(TOperation operation, DomainResource resource);
    }
}