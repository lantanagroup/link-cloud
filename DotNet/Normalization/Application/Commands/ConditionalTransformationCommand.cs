using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Collections.Generic;
using static Hl7.Fhir.Model.Encounter;

namespace LantanaGroup.Link.Normalization.Application.Commands
{
    public class ConditionalTransformationCommand : IRequest<OperationCommandResult>
    {
        public Base Resource { get; set; }

        public ConditionalTransformationOperation Operation { get; set; }
        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class ConditionalTransformationHandler : IRequestHandler<ConditionalTransformationCommand, OperationCommandResult>
    {
        private readonly ILogger<ConditionalTransformationHandler> _logger;
        private readonly IConditionalTransformationEvaluationService _conditionalTransformationEvaluationService;

        public ConditionalTransformationHandler(ILogger<ConditionalTransformationHandler> logger, IConditionalTransformationEvaluationService conditionalTransformationEvaluationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _conditionalTransformationEvaluationService = conditionalTransformationEvaluationService ?? throw new ArgumentNullException(nameof(conditionalTransformationEvaluationService));
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

        public async Task<OperationCommandResult> Handle(ConditionalTransformationCommand request, CancellationToken cancellationToken)
        {
            Base resource = request.Resource;

            var operationCommandResult = new OperationCommandResult();


            List<(Base? ele, Base resource)> selectedElements = new List<(Base? ele, Base resource)>();

            selectedElements.AddRange(resource.Select(request.Operation.TransformElement).ToList().Select(x => (x, resource)));

            selectedElements.ForEach(x => assignElement(x.ele, x.resource, request));

            operationCommandResult.PropertyChanges = request.PropertyChanges;
            operationCommandResult.Resource = resource;
            return operationCommandResult;
        }
        private void assignElement(Base eleToUpdate, Base resource, ConditionalTransformationCommand request)
        {
            var evaluationOutcome = _conditionalTransformationEvaluationService.Evaluate(request.Operation, (Resource)resource);

            if (evaluationOutcome.AllConditionsMet)
            {
                var oldElementValue = eleToUpdate.ToString();
                if (eleToUpdate is Code)
                {
                    ((Code)eleToUpdate).Value = request.Operation.TransformValue;
                }
                else if(eleToUpdate is Code<EncounterStatus>)
                {
                    ((Code<EncounterStatus>)eleToUpdate).Value = (EncounterStatus)Enum.Parse(typeof(EncounterStatus), request.Operation.TransformValue, true);
                }
                else
                {
                    eleToUpdate = new FhirString(request.Operation.TransformValue);
                }

                request.PropertyChanges.Add(new PropertyChangeModel
                {
                    InitialPropertyValue = oldElementValue,
                    NewPropertyValue = eleToUpdate.ToString(),
                    PropertyName = $"{request.Operation.TransformResource}.{request.Operation.TransformElement}"
                });

            }
        }
    }

}
