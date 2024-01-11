using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using static Hl7.Fhir.Model.Encounter;

namespace LantanaGroup.Link.Normalization.Application.Commands
{
    public class ConditionalTransformationCommand : IRequest<OperationCommandResult>
    {
        public Bundle Bundle { get; set; }
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

        public async Task<OperationCommandResult> Handle(ConditionalTransformationCommand request, CancellationToken cancellationToken)
        {
            Bundle bundle = request.Bundle;
            var operationCommandResult = new OperationCommandResult();

            var selectedResources = bundle.Select($"Bundle.entry.resource.ofType({request.Operation.TransformResource})").ToList();

            List<(Base? ele, Base resource)> selectedElements = new List<(Base? ele, Base resource)>();

            selectedResources.ForEach(resource =>
            {
                selectedElements.AddRange(resource.Select(request.Operation.TransformElement).ToList().Select(x => (x, resource)));
            });

            selectedElements.ForEach(x => assignElement(x.ele, x.resource, request));

            operationCommandResult.PropertyChanges = request.PropertyChanges;
            operationCommandResult.Bundle = bundle;
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
                    ((Code<EncounterStatus>)eleToUpdate).Value = (EncounterStatus)Enum.Parse(typeof(EncounterStatus), request.Operation.TransformValue);
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
