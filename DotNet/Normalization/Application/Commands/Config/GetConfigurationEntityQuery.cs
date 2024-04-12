    using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config;

public class GetConfigurationEntityQuery : IRequest<NormalizationConfigEntity>
{
    public string FacilityId { get; set; }
}

public class GetConfigurationEntityQueryHandler : IRequestHandler<GetConfigurationEntityQuery, NormalizationConfigEntity>
{
    private readonly ILogger<GetConfigurationEntityQueryHandler> _logger;
    private readonly IConfigRepository _tenantNormalizationConfigService;

    public GetConfigurationEntityQueryHandler(ILogger<GetConfigurationEntityQueryHandler> logger, IConfigRepository tenantNormalizationConfigService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantNormalizationConfigService = tenantNormalizationConfigService ?? throw new ArgumentNullException(nameof(tenantNormalizationConfigService));
    }

    public async Task<NormalizationConfigEntity> Handle(GetConfigurationEntityQuery request, CancellationToken cancellationToken)
    {
        NormalizationConfigEntity config = await _tenantNormalizationConfigService.GetAsync(request.FacilityId, cancellationToken);

        if (config == null)
        {
            var message = $"Config for tenant ID {request.FacilityId} does not exist.";
            _logger.LogCritical(message);
            throw new NoEntityFoundException();
        }


        return config;
    }
}