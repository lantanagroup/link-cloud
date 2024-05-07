using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config;

public class GetConfigurationEntityQuery : IRequest<NormalizationConfig>
{
    public string FacilityId { get; set; }
}

public class GetConfigurationEntityQueryHandler : IRequestHandler<GetConfigurationEntityQuery, NormalizationConfig>
{
    private readonly ILogger<GetConfigurationEntityQueryHandler> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    //private readonly NormalizationDbContext _dbContext;

    public GetConfigurationEntityQueryHandler(ILogger<GetConfigurationEntityQueryHandler> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        //_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<NormalizationConfig> Handle(GetConfigurationEntityQuery request, CancellationToken cancellationToken)
    {
        var _dbContext = _serviceScopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<NormalizationDbContext>();

        NormalizationConfig? config = await _dbContext.NormalizationConfigs.FirstOrDefaultAsync(c => c.FacilityId == request.FacilityId, cancellationToken);   

        if (config == null)
        {
            var message = $"Config for tenant ID {request.FacilityId} does not exist.";
            _logger.LogCritical(message);
            throw new NoEntityFoundException();
        }


        return config;
    }
}