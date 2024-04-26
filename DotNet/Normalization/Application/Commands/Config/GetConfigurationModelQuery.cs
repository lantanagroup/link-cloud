using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config
{
    public class GetConfigurationModelQuery : IRequest<NormalizationConfigModel>
    {
        public string FacilityId { get; set; }
    }

    public class GetConfigurationModelQueryHandler : IRequestHandler<GetConfigurationModelQuery, NormalizationConfigModel>
    {
        private readonly ILogger<GetConfigurationModelQueryHandler> _logger;
        private readonly NormalizationDbContext _dbContext;

        public GetConfigurationModelQueryHandler(NormalizationDbContext dbContext, ILogger<GetConfigurationModelQueryHandler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<NormalizationConfigModel> Handle(GetConfigurationModelQuery request, CancellationToken cancellationToken)
        {
            NormalizationConfig? config = await _dbContext.NormalizationConfigs.FirstOrDefaultAsync(c => c.FacilityId == request.FacilityId, cancellationToken);

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
