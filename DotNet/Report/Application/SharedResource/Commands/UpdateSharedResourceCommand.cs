using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.SharedResource.Commands
{
    public class UpdateSharedResourceCommand : IRequest<SharedResourceModel>
    {
        public SharedResourceModel SharedResource { get; private set; }
        public Resource UpdatedResource { get; private set; }

        public UpdateSharedResourceCommand(SharedResourceModel sharedResource, Resource updatedResource) 
        {  
            SharedResource = sharedResource;
            UpdatedResource = updatedResource;
        }
    }

    public class UpdateSharedResourceCommandHandler : IRequestHandler<UpdateSharedResourceCommand, SharedResourceModel> 
    {
        private readonly SharedResourceRepository _repository;

        public UpdateSharedResourceCommandHandler(SharedResourceRepository repository) 
        { 
            _repository = repository;
        }

        public async Task<SharedResourceModel> Handle(UpdateSharedResourceCommand request, CancellationToken cancellationToken)
        {
            request.SharedResource.ModifyDate = DateTime.UtcNow;
            request.SharedResource.Resource = request.UpdatedResource; //JsonSerializer.Serialize(request.UpdatedResource, new JsonSerializerOptions().ForFhir());

            var sharedResource = await _repository.UpdateAsync(request.SharedResource);

            return sharedResource;
        }
    }
}
