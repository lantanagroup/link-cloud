using Hl7.Fhir.Model;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public interface IOperation
    {
        [DataMember]
        OperationType OperationType { get; }

        [DataMember]
        string Name { get; set; }
    }
}
