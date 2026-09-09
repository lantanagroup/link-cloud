using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class HSLOCMapOperationService : BaseOperationService<HSLOCMapOperation>
    {
        public HSLOCMapOperationService(ILogger<HSLOCMapOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
        }

        protected override Task<OperationResult> ExecuteOperation(HSLOCMapOperation operation, DomainResource resource, List<DomainResource>? supportingResources = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationResult.Failure("HSLOCMap operation execution is not implemented yet.", resource));
        }
    }
}