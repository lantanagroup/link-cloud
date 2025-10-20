using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Operations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class TestOperationModel()
    {
        [BindRequired]
        [Required, DataMember]
        public required IOperation Operation { get; set; }

        [BindRequired]
        [Required, DataMember]
        public DomainResource? Resource { get; set; }

    }
}
