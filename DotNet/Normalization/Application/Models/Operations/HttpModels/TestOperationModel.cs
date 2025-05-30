using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Operations;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class TestOperationModel()
    {
        [DataMember]
        public IOperation? Operation { get; set; }
        [Required]
        [DataMember]
        public string? Resource { get; set; }

    }
}
