using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands
{
    public class CopyLocationIdentifierToTypeCommand : IRequest<OperationCommandResult>
    {
        public Base Resource { get; set; }

        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class CopyLocationIdentifierToTypeHandler : IRequestHandler<CopyLocationIdentifierToTypeCommand, OperationCommandResult>
    {

        public async Task<OperationCommandResult> Handle(CopyLocationIdentifierToTypeCommand request, CancellationToken cancellationToken)
        {
            var resource = request.Resource;
            var propertyChanges = request.PropertyChanges;
            if (resource.TypeName == ResourceType.Location.ToString())
            {
                var locationResource = (Location)resource;
                var identifiers = locationResource.Identifier;
                var types = locationResource.Type;
                if (identifiers.Count > 0)
                {
                    foreach (var identifier in identifiers)
                    {
                        var codings = new List<Coding>();
                        var type = new CodeableConcept();
                        var coding = new Coding();
                        coding.Code = identifier.Value;
                        coding.System = identifier.System;

                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(coding.Code),
                            NewPropertyValue = coding.Code,
                        });
                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(coding.System),
                            NewPropertyValue = coding.System,
                        });
                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(codings),
                            InitialPropertyValue = codings?.ToString(),
                        });

                        codings.Add(coding);

                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(codings),
                            NewPropertyValue = codings?.ToString(),
                        });

                        type.Coding = codings;

                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(type.Coding),
                            NewPropertyValue = type.Coding.ToString(),
                        });

                        types.Add(type);
                    }
                    var locationType = ((Location)resource).Type;
                    propertyChanges.Add(new PropertyChangeModel
                    {
                        PropertyName = nameof(locationType),
                        InitialPropertyValue = locationType?.ToString(),
                        NewPropertyValue = types?.ToString()
                    });
                    ((Location)resource).Type = types;
                }
          
            }

            return new OperationCommandResult
            {
                Resource = resource,
                PropertyChanges = propertyChanges,
            };
        }
    }
}
