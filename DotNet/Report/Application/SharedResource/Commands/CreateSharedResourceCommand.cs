using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.SharedResource.Commands
{
    public class CreateSharedResourceCommand : IRequest<SharedResourceModel>
    {
        public string FacilityId { get; private set; }
        public Resource Resource { get; private set; }

        public CreateSharedResourceCommand(string facilityId, Resource resource)
        { 
            FacilityId = facilityId;
            Resource = resource;
        }
    }

    public class CreateSharedResourceCommandHandler : IRequestHandler<CreateSharedResourceCommand, SharedResourceModel> 
    {
        private readonly SharedResourceRepository _repository;

        public CreateSharedResourceCommandHandler(SharedResourceRepository repository)
        {
            _repository = repository;
        }

        public async Task<SharedResourceModel> Handle(CreateSharedResourceCommand request, CancellationToken cancellationToken)
        {
            var sharedResource = new SharedResourceModel()
            {
                CreateDate = DateTime.UtcNow,
                FacilityId = request.FacilityId,
                Resource = JsonSerializer.Serialize<Resource>(request.Resource, new JsonSerializerOptions().ForFhir()),
                ResourceType = request.Resource.TypeName,
                ResourceId = request.Resource.Id

            };

            await _repository.AddAsync(sharedResource);

            return sharedResource;
        }
    }
}
