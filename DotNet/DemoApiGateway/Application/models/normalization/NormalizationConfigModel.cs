using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.normalization
{
    public class NormalizationConfigModel
    {
        [Required]
        public string FacilityId { get; set; }
        [Required]
        public Dictionary<string, INormalizationOperation> OperationSequence { get; set; }
    }  
    
    public class ConceptMapOperation : INormalizationOperation
    {
        public string FacilityId { get; set; }
        public string Name { get; set; }
        public object? FhirConceptMap { get; set; }
        public string FhirPath { get; set; }
        public string FhirContext { get; set; }
    }

    public class CopyElementOperation : INormalizationOperation
    {
        public string FacilityId { get; set; }
        public string Name { get; set; }
        public string FhirPath { get; set; }
        public string FhirContext { get; set; }
        public string TargetFhirPath { get; set; }
        public string TargetFhirContext { get; set; }
    }

    public class CopyLocationIdentifierToTypeOperation : INormalizationOperation
    {
        public string FacilityId { get; set; }
        public string Name { get; set; }
        public string FhirPath { get; set; }
        public string FhirContext { get; set; }
        public string TargetFhirPath { get; set; }
        public string TargetFhirContext { get; set; }
    }
   
    public class ConditionalTransformationOperation : INormalizationOperation
    {
        public string FacilityId { get; set; }
        public string Name { get; set; }
        public List<ConditionElement> Conditions { get; set; }
        public string TransformResource { get; set; }
        public string TransformElement { get; set; }
        public string TransformValue { get; set; }
    }

    public class ConditionElement
    {
        public string FacilityId { get; set; }
        public string Name { get; set; }
        public string Element { get; set; }
        public Operators Operator { get; set; }
        public string OperatorValue { get; set; }
    }

    public enum Operators
    {
        Equal, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual
    }

}
