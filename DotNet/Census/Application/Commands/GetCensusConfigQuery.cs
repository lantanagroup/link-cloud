using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using MediatR;

namespace LantanaGroup.Link.Census.Application.Commands
{
    public class GetCensusConfigQuery : IRequest<CensusConfigModel>
    {
        public string FacilityId { get; set; }
    }

    public class GetCensusConfigQueryHandler : IRequestHandler<GetCensusConfigQuery, CensusConfigModel>
    {
        private readonly ILogger<GetCensusConfigQueryHandler> _logger;
        private readonly ICensusConfigRepository _repository;

        public GetCensusConfigQueryHandler(ILogger<GetCensusConfigQueryHandler> logger, ICensusConfigRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<CensusConfigModel> Handle(GetCensusConfigQuery request, CancellationToken cancellationToken)
        {
            var configEntity = await _repository.GetByFacilityIdAsync(request.FacilityId, cancellationToken);

            if (configEntity == null)
            {
                _logger.LogInformation($"No record found for facilityId {request?.FacilityId}");
                return null;
            }

            _logger.LogInformation($"Retrieved config for {configEntity?.FacilityID}");

            return new CensusConfigModel
            {
                FacilityId = configEntity?.FacilityID,
                ScheduledTrigger = configEntity?.ScheduledTrigger
            };
        }
    }
}
