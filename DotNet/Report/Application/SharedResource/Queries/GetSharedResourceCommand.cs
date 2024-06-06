using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.SharedResource.Queries
{
    public class GetSharedResourceCommand : IRequest<SharedResourceModel>
    {
        public string Id { get; private set; }        

        public GetSharedResourceCommand(string id)
        {
            Id = id;
        }
    }

    public class GetSharedResourceCommandHandler : IRequestHandler<GetSharedResourceCommand, SharedResourceModel>
    {
        private readonly SharedResourceRepository _repository;
        private readonly ILogger<GetSharedResourceCommandHandler> _logger;
        public GetSharedResourceCommandHandler(ILogger<GetSharedResourceCommandHandler> logger, SharedResourceRepository repository)
        {
            _repository = repository;
            _logger = logger;
        }

        public Task<SharedResourceModel> Handle(GetSharedResourceCommand request, CancellationToken cancellationToken)
        {
            try
            {
                return _repository.GetAsync(request.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Occurred in GetSharedResourceCommandHandler");
                throw;
            }
        }
    }
}
