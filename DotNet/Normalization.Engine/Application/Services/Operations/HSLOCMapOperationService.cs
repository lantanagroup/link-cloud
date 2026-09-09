using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class HSLOCMapOperationService : BaseOperationService<HSLOCMapOperation>
    {
        private readonly CodeMapOperationService _codeMapOperationService;
        private readonly CopyLocationOperationService _copyLocationOperationService;
        private readonly CopyLocationAliasToTypeIterativelyOperationService _copyLocationAliasToTypeIterativelyOperationService;

        public HSLOCMapOperationService(ILogger<HSLOCMapOperationService> logger,
                                        CodeMapOperationService codeMapOperationService,
                                        CopyLocationOperationService copyLocationOperationService,
                                        CopyLocationAliasToTypeIterativelyOperationService copyLocationAliasToTypeIterativelyOperationService,
                                        TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
            _codeMapOperationService = codeMapOperationService;
            _copyLocationOperationService = copyLocationOperationService;
            _copyLocationAliasToTypeIterativelyOperationService = copyLocationAliasToTypeIterativelyOperationService;
        }

        protected override async Task<OperationResult> ExecuteOperation(HSLOCMapOperation operation, DomainResource resource, List<DomainResource>? supportingResources = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //Execute CopyLocationOperation to copy location.identifier to location.type. 
            //This is typically how HSLOC codes are mapped in Epic
            await _copyLocationOperationService.ProcessOperationAsync(
                new CopyLocationOperation(),
                resource,
                supportingResources,
                cancellationToken);

            //Execute CopyLocationAliasToTypeIterativelyOperation to copy location.alias to location.type. 
            //This is typically how HSLOC codes are mapped in Cerner/Oracle
            await _copyLocationAliasToTypeIterativelyOperationService.ProcessOperationAsync(
                new CopyLocationAliasToTypeIterativelyOperation(),
                resource,
                supportingResources,
                cancellationToken);

            //Now that location.type is normalized, execute the code map.
            var codeMapOperation = new CodeMapOperation(
            operation.Name,
            operation.FhirPath,
            operation.CodeSystemMaps,
            operation.Description);

            var result = await _codeMapOperationService.ProcessOperationAsync(
                codeMapOperation,
                resource,
                supportingResources,
                cancellationToken);
            
            return result;
        }
    }
}