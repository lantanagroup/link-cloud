using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.SharedResource.Queries
{
    public class GetSharedResourceCommand : IRequest<SharedResourceModel>
    {
        public string FacilityId { get; private set; }
        public string ResourceType { get; private set; }
        public string ResourceId { get; private set; }

        public GetSharedResourceCommand(string facilityId, string resourceType, string resourceId)
        {
            FacilityId = facilityId;
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }

    public class GetSharedResourceCommandHandler : IRequestHandler<GetSharedResourceCommand, SharedResourceModel>
    {
        private readonly SharedResourceRepository _repository;

        public Task<SharedResourceModel> Handle(GetSharedResourceCommand request, CancellationToken cancellationToken)
        {
            return _repository.GetAsync(request.FacilityId, request.ResourceId, request.ResourceType);
        }
    }
}
