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

        public GetSharedResourceCommandHandler(SharedResourceRepository repository)
        {
            _repository = repository;
        }

        public Task<SharedResourceModel> Handle(GetSharedResourceCommand request, CancellationToken cancellationToken)
        {
            return _repository.GetAsync(request.Id);
        }
    }
}
