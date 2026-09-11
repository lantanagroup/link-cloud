using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class RemoveExtensionsOperationService : BaseOperationService<RemoveExtensionsOperation>
    {
        private readonly ILogger<RemoveExtensionsOperationService> _logger;

        public RemoveExtensionsOperationService(ILogger<RemoveExtensionsOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
            _logger = logger;
        }

        protected override Task<OperationResult> ExecuteOperation(RemoveExtensionsOperation operation, DomainResource resource, List<DomainResource>? supportingResources = null, CancellationToken cancellationToken = default)
        {
            if (resource.Extension == null || resource.Extension.Count == 0)
            {
                return Task.FromResult(OperationResult.NoAction("Resource has no extensions.", resource));
            }

            var urlSet = new HashSet<string>(operation.ExtensionUrls, StringComparer.Ordinal);
            int removedCount = resource.Extension.RemoveAll(e => e.Url != null && urlSet.Contains(e.Url));

            if (removedCount == 0)
            {
                return Task.FromResult(OperationResult.NoAction("No extensions matched the configured URLs.", resource));
            }

            _logger.LogDebug(
                "Removed {Count} extension(s) from {ResourceType}/{ResourceId}",
                removedCount,
                resource.TypeName.SanitizeForLog(),
                resource.Id.SanitizeForLog());

            return Task.FromResult(OperationResult.Success(resource));
        }
    }
}
