using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config
{
    public class GetConfigurationModelQuery : IRequest<NormalizationConfigModel>
    {
        public string FacilityId { get; set; }
    }

    public class GetConfigurationModelQueryHandler : IRequestHandler<GetConfigurationModelQuery, NormalizationConfigModel>
    {
        private readonly ILogger<GetConfigurationModelQueryHandler> _logger;
        private readonly IConfigRepository _tenantNormalizationConfigService;

        public GetConfigurationModelQueryHandler(IConfigRepository tenantNormalizationConfigService, ILogger<GetConfigurationModelQueryHandler> logger)
        {
            _tenantNormalizationConfigService = tenantNormalizationConfigService;
            _logger = logger;
        }

        public async Task<NormalizationConfigModel> Handle(GetConfigurationModelQuery request, CancellationToken cancellationToken)
        {
            NormalizationConfigEntity config = await _tenantNormalizationConfigService.GetAsync(request.FacilityId);

            if (config == null)
            {
                var message = $"Config for tenant ID {request.FacilityId} does not exist.";
                _logger.LogCritical(message);
                throw new NoEntityFoundException();
            }

            var configModel = new NormalizationConfigModel
            {
                FacilityId = config.FacilityId,
                OperationSequence = config.OperationSequence
            };

            return configModel;
        }
    }
}
