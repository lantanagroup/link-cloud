using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands;

public class PeriodDateFixerCommand : IRequest<OperationCommandResult>
{
    public Bundle Bundle { get; set; }
    public List<PropertyChangeModel> PropertyChanges { get; set; }
}

public class PeriodDateFixerCommandHandler : IRequestHandler<PeriodDateFixerCommand, OperationCommandResult>
{
    public async Task<OperationCommandResult> Handle(PeriodDateFixerCommand request, CancellationToken cancellationToken)
    {
        var bundle = request.Bundle;
        var propertyChanges = request.PropertyChanges;
        var operationCommandResult = new OperationCommandResult();

        foreach (var entry in bundle.Entry)
        {
            var resource = entry.Resource;

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

        operationCommandResult.PropertyChanges = request.PropertyChanges;
        operationCommandResult.Bundle = bundle;
        return operationCommandResult;
    }
}
