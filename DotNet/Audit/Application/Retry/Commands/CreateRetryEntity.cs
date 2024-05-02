using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Retry.Commands
{
    public class CreateRetryEntity : ICreateRetryEntity
    {
        private readonly ILogger<CreateRetryEntity> _logger;
        private readonly IRetryRepository _retryRepository;

        public CreateRetryEntity(ILogger<CreateRetryEntity> logger, IRetryRepository retryRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryRepository = retryRepository ?? throw new ArgumentNullException(nameof(retryRepository));
        }

        /// <summary>
        /// A command to create a retry entity
        /// </summary>
        /// <param name="model"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> Execute(RetryEntity? model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance
                .StartActivityWithTags("CreateRetryEntity.Execute",
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.Service, model?.ServiceName),
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, model?.FacilityId)
                ]);

            ArgumentNullException.ThrowIfNull(model);

            try
            {
                await _retryRepository.AddAsync(model, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogRetryEntityCreationException(ex.Message);
                return false;
            }

        }
    }
}
