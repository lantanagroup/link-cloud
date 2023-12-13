using Amazon.Runtime.Internal;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands
{
    public class CopyElementCommand : IRequest<OperationCommandResult>
    {
        public Bundle Bundle { get; set; }
        public CopyElementOperation Operation { get; set; }
        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class CopyElementHandler : IRequestHandler<CopyElementCommand, OperationCommandResult>
    {
        public async Task<OperationCommandResult> Handle(CopyElementCommand request, CancellationToken cancellationToken)
        {
            var bundle = request.Bundle;
            var propertyChanges = request.PropertyChanges;

            var resourceTypes = new List<ResourceType>();
            try
            {
                request.Operation.Resources.ToList().ForEach(resource =>
                {
                    resourceTypes.Add(GetFhirResourceType(request.Operation.Resources));
                });
            }
            catch (Exception ex)
            {
                //add audit event and other error handling and toss exception to caller
                throw ex;
            }

            foreach (var entry in bundle.Entry)
            {
                foreach (var resourceType in resourceTypes)
                {
                    if (entry.Resource.TypeName == resourceType.ToString())
                    {
                        // todo: implement me!
                        // will most likely need to use reflection to cast entry.Resource to the type defined in config
                        // and then use reflection (again) to pull the appropriate property for that type.
                        // config may need to change so that we know the type of the toElement in case we need to create a new object
                        // like in CopyLocationIdentifierToTypeCommand
                    }
                }
            }

            return new OperationCommandResult
            {
                Bundle = bundle,
                PropertyChanges = propertyChanges
            };
        }

        private ResourceType GetFhirResourceType(string requestedResource)
        {
            var success = Enum.TryParse(typeof(ResourceType), requestedResource, out var resourceType);
            if (success)
            {
                return (ResourceType)resourceType;
            }
            throw new Exception($"Resource Not found for name {requestedResource}");
        }
    }
}
