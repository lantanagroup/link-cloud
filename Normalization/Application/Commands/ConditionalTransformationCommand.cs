using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;

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

        public ConditionalTransformationHandler(ILogger<ConditionalTransformationHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OperationCommandResult> Handle(ConditionalTransformationCommand request, CancellationToken cancellationToken)
        {
            Bundle bundle = request.Bundle;
            var operationCommandResult = new OperationCommandResult();
            var resourceToSearch = request.Operation.TransformResource;
            var elementToSearch = request.Operation.TransformElement;
            var conditions = request.Operation.Conditions;

            foreach (var item in bundle.Entry)
            {
                var resource = item.Resource;

                //encounter status transformation
                if (resource.TypeName.Equals("Encounter", StringComparison.OrdinalIgnoreCase)
                    && resourceToSearch.Equals("Encounter", StringComparison.OrdinalIgnoreCase)
                    && elementToSearch.Equals("Status", StringComparison.OrdinalIgnoreCase))
                {
                    var encounter = (Encounter)resource;
                    if ((!encounter.Status.HasValue
                        || (encounter.Status.HasValue && encounter.Status != Encounter.EncounterStatus.Finished))
                        && !string.IsNullOrWhiteSpace(encounter.Period.End))
                    {
                        var oldStatus = encounter.Status;
                        encounter.Status = Encounter.EncounterStatus.Finished;
                        var oldStatusStr = oldStatus == null ? "NoValue" : oldStatus.ToString();
                        if(encounter.Meta == null)
                        {
                            encounter.Meta = new Meta();
                        }
                        encounter.Meta.AddExtension(NormalizationConstants.Extensions.OriginalElementValueExtension, new FhirString(oldStatusStr));
                        request.PropertyChanges.Add(new PropertyChangeModel
                        {
                            InitialPropertyValue = oldStatus.ToString(),
                            NewPropertyValue = "Finished",
                            PropertyName = "Encounter.Status"
                        });
                    }
                }
                //fix period dates
                else if (elementToSearch.Equals("Period"))
                {
                    
                    foreach (var ele in resource.NamedChildren.Where(x => x.ElementName.Equals("Period", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        var period = (Period)ele.Value;
                        var endDate = period.EndElement;
                        var startDate = period.StartElement;

                        if (endDate != null && (endDate.Value.Length != startDate.Value.Length))
                        {
                            DateTime.TryParse(endDate.Value, out DateTime endDateTime);
                            DateTime.TryParse(startDate.Value, out DateTime startDateTime);

                            period.EndElement.Value = endDateTime.ToString("yyyy-MM-ddThh:mm:ss") + "Z";
                            period.StartElement.Value = startDateTime.ToString("yyyy-MM-ddThh:mm:ss") + "Z";
                        }

                    }
                }
                else if (elementToSearch.Equals(nameof(CodeableConcept), System.StringComparison.OrdinalIgnoreCase) ||
                        elementToSearch.Equals(nameof(Coding), System.StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var element in resource.Children)
                    {
                        if (element.GetType() == typeof(CodeableConcept))
                        {
                            var codeableConcept = (CodeableConcept)element;
                            var condition = conditions.FirstOrDefault();
                            if (condition != null)
                            {
                                var codings = codeableConcept.Coding.Where(x => x.System.Equals(condition.OperatorValue, System.StringComparison.OrdinalIgnoreCase));
                                if (codings.Count() > 0)
                                {
                                    foreach (var code in codings)
                                    {
                                        var oldSystem = code.System;
                                        code.System = request.Operation.TransformValue;
                                        code.AddExtension(NormalizationConstants.Extensions.OriginalElementValueExtension, new FhirUri(oldSystem));
                                        request.PropertyChanges.Add(new PropertyChangeModel
                                        {
                                            InitialPropertyValue = oldSystem,
                                            NewPropertyValue = code.System,
                                            PropertyName = "CodeableConcept.Coding.System"
                                        });
                                    }
                                }
                            }
                        }
                        else if (resource.GetType() == typeof(Coding))
                        {
                            var coding = (Coding)element;
                            var condition = conditions.FirstOrDefault();
                            if (condition != null)
                            {
                                if (coding.System.Equals(condition.OperatorValue, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    var oldSystem = coding.System;
                                    coding.System = request.Operation.TransformValue;
                                    coding.AddExtension(NormalizationConstants.Extensions.OriginalElementValueExtension, new FhirUri(oldSystem));
                                    request.PropertyChanges.Add(new PropertyChangeModel
                                    {
                                        InitialPropertyValue = oldSystem,
                                        NewPropertyValue = coding.System,
                                        PropertyName = "Coding.System"
                                    });
                                }
                            }
                        }
                    }
                }
            }
            operationCommandResult.PropertyChanges = request.PropertyChanges;
            operationCommandResult.Bundle = bundle;
            return operationCommandResult;
        }
    }
}
