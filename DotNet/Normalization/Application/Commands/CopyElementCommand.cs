using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands
{
    public class CopyElementCommand : IRequest<OperationCommandResult>
    {
        public Base Resource { get; set; }
        public CopyElementOperation Operation { get; set; }
        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class CopyElementHandler : IRequestHandler<CopyElementCommand, OperationCommandResult>
    {
        public async Task<OperationCommandResult> Handle(CopyElementCommand request, CancellationToken cancellationToken)
        {
            var resource = request.Resource;
            var propertyChanges = request.PropertyChanges;
 
            var resourceType = GetFhirResourceType(resource.TypeName);
            var fromFhirPath = request.Operation.FromFhirPath;
            var toFhirPath = request.Operation.ToFhirPath;
            var fromElement = resource.Select(fromFhirPath).FirstOrDefault();
            var toElement = resource.Select(toFhirPath).FirstOrDefault();
            if (fromElement != null && toElement != null)
            {
                if (toElement is Code)
                {
                    assignElement(fromElement, (Code)toElement);
                }
                else
                {
                    toElement = new FhirString(fromElement.ToString());
                }
                propertyChanges.Add(new PropertyChangeModel
                {
                    InitialPropertyValue = toElement?.ToString(),
                    NewPropertyValue = fromElement?.ToString(),
                    PropertyName = $"{resourceType}.{toFhirPath}"
                });
            }

            return new OperationCommandResult
            {
                Resource = resource,
                PropertyChanges = propertyChanges
            };
        }

        private void assignElement(Base? fromElement, Code toElement)
        {
            if (fromElement is Code)
            {
                toElement.Value = ((Code)fromElement).Value;
            }
            else
            {
                toElement.Value = fromElement?.ToString();
            }
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
