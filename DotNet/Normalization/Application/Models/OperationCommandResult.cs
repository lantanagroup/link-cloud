using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Normalization.Application.Models;

public class OperationCommandResult
{
    public Base Resource { get; set; }

   // public Bundle Bundle { get; set; }
    public List<PropertyChangeModel> PropertyChanges { get; set; }
}
