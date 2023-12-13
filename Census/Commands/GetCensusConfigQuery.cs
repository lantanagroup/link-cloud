using Census.Models;
using LantanaGroup.Link.Census.Application.Interfaces;
using MediatR;

namespace Census.Commands
{
    public class GetCensusConfigQuery : IRequest<CensusConfigModel>
    {
        public string FacilityId { get; set; }
    }

    public class GetCensusConfigQueryHandler : IRequestHandler<GetCensusConfigQuery, CensusConfigModel>
    {
        private readonly ILogger<GetCensusConfigQueryHandler> _logger;
        private readonly ICensusConfigMongoRepository _repository;

        public GetCensusConfigQueryHandler(ILogger<GetCensusConfigQueryHandler> logger, ICensusConfigMongoRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<CensusConfigModel> Handle(GetCensusConfigQuery request, CancellationToken cancellationToken)
        {
            var configEntity = await _repository.GetAsync(request.FacilityId, cancellationToken);

            if(configEntity == null)
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
