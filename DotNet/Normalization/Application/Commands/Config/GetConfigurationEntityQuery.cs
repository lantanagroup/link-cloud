using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config;

public class GetConfigurationEntityQuery : IRequest<NormalizationConfig>
{
    public string FacilityId { get; set; }
}

public class GetConfigurationEntityQueryHandler : IRequestHandler<GetConfigurationEntityQuery, NormalizationConfig>
{
    private readonly ILogger<GetConfigurationEntityQueryHandler> _logger;
    private readonly NormalizationDbContext _dbContext;

    public GetConfigurationEntityQueryHandler(ILogger<GetConfigurationEntityQueryHandler> logger, NormalizationDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<NormalizationConfig> Handle(GetConfigurationEntityQuery request, CancellationToken cancellationToken)
    {
        NormalizationConfig config = await _dbContext.NormalizationConfigs.FirstAsync(c => c.FacilityId == request.FacilityId);   

        if (config == null)
        {
            var message = $"Config for tenant ID {request.FacilityId} does not exist.";
            _logger.LogCritical(message);
            throw new NoEntityFoundException();
        }


        return config;
    }
}