using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    /// <summary>
    /// A background service that executes code mapping operations on FHIR resources asynchronously via a queue.
    /// </summary>
    public class CodeMapOperationService : BackgroundService
    {
        // Thread-safe queue for operations with result tasks
        private readonly ConcurrentQueue<(CodeMapOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)> _operationQueue = new();

        // Configurable timeout for operations
        private readonly TimeSpan _operationTimeout;

        // Logger for diagnostic and error logging
        private readonly ILogger<CodeMapOperationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeMapOperationService"/> class.
        /// </summary>
        /// <param name="logger">The logger for diagnostic and error logging.</param>
        /// <param name="operationTimeout">The timeout for queued operations. Defaults to 120 seconds.</param>
        public CodeMapOperationService(ILogger<CodeMapOperationService> logger, TimeSpan? operationTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(120);
        }

        /// <summary>
        /// Enqueues a code mapping operation for asynchronous execution and returns a task to await the result.
        /// </summary>
        /// <param name="operation">The code mapping operation to execute.</param>
        /// <param name="resource">The FHIR resource to operate on.</param>
        /// <returns>A task that completes with the operation result.</returns>
        public async Task<OperationResult> EnqueueOperationAsync(CodeMapOperation operation, DomainResource resource)
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
                _logger.LogError(tex, "Code mapping operation '{OperationName}' timed out after {Timeout}.", operation.Name, _operationTimeout);
                return OperationResult.Failure($"Code mapping operation '{operation.Name}' timed out after {_operationTimeout}.");
            }
        }

        /// <summary>
        /// Executes the background service, processing queued code mapping operations in batches.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop the service.</param>
        /// <returns>A task representing the service's execution.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<(CodeMapOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)>();
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

        /// <summary>
        /// Processes a single code mapping operation.
        /// </summary>
        /// <param name="operation">The code mapping operation to execute.</param>
        /// <param name="resource">The FHIR resource to operate on.</param>
        /// <returns>The result of the operation.</returns>
        private OperationResult ProcessOperation(CodeMapOperation operation, DomainResource resource)
        {
            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
            {
                return OperationResult.Failure($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");
            }

            try
            {
                var modifiedResource = Execute(operation, resourceCopy);
                return OperationResult.Success(modifiedResource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process code mapping operation '{OperationName}' for resource type {ResourceType}.", operation.Name, resource.TypeName);
                return OperationResult.Failure($"Failed to process code mapping operation '{operation.Name}': {ex.Message}", resourceCopy);
            }
        }

        /// <summary>
        /// Executes the code mapping operation on the provided resource.
        /// </summary>
        /// <param name="operation">The code mapping operation.</param>
        /// <param name="domainResource">The FHIR resource to modify.</param>
        /// <returns>The modified resource.</returns>
        private DomainResource Execute(CodeMapOperation operation, DomainResource domainResource)
        {
            var source = domainResource.Select(operation.FhirPath).FirstOrDefault();

            if (source == null)
            {
                return domainResource;
            }

            if (source.GetType() == typeof(Coding))
            {
                UpdateCoding((Coding)source, operation.CodeSystemMaps);
            }
            else if (source.GetType() == typeof(CodeableConcept))
            {
                var codeableConcept = (CodeableConcept)source;
                foreach (var coding in codeableConcept.Coding)
                {
                    UpdateCoding(coding, operation.CodeSystemMaps);
                }
            }
            else
            {
                _logger.LogWarning("Unsupported source type {SourceType} for FHIRPath {FhirPath} in operation {OperationName}.", source.GetType().Name, operation.FhirPath, operation.Name);
            }

            return domainResource;
        }

        /// <summary>
        /// Updates a coding element based on the provided code system maps.
        /// </summary>
        /// <param name="coding">The coding element to update.</param>
        /// <param name="codeSystemMaps">The list of code system mappings.</param>
        private void UpdateCoding(Coding coding, List<CodeSystemMap> codeSystemMaps)
        {
            var codeSystemMap = codeSystemMaps.FirstOrDefault(x => x.SourceSystem == coding.System);

            if (codeSystemMap == null)
            {
                return;
            }

            if (codeSystemMap.CodeMaps.TryGetValue(coding.Code, out var matchingCodeMap))
            {
                coding.System = codeSystemMap.TargetSystem;
                coding.Code = matchingCodeMap.Code;
                coding.Display = matchingCodeMap.Display;
            }
        }
    }
}