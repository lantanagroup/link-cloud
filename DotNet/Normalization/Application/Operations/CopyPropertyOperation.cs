using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Hl7.Fhir.Validation;
using Hl7.FhirPath;
using NormalizationPOC.Helpers;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Serialization;
using NormalizationPOC.Interfaces;

namespace NormalizationPOC.Operations
{
    public class CopyPropertyOperation : IOperation
    {
        public string OperationType => "CopyProperty";
        public string Name { get; private set; }
        public string SourceFhirPath { get; private set; }
        public string TargetFhirPath { get; private set; }

        public CopyPropertyOperation(string name, string sourceFhirPath, string targetFhirPath)
        {
            Name = name;
            SourceFhirPath = sourceFhirPath;
            TargetFhirPath = targetFhirPath;
        }

        public DomainResource Execute(DomainResource domainResource)
        {
            var source = domainResource.Select(this.SourceFhirPath).FirstOrDefault();
            var target = PropertyHelper.GetProperty(domainResource, this.TargetFhirPath);

            PropertyInfo propertyInfo = typeof(FhirString).GetProperty("Value");
            string source_string = (string)propertyInfo.GetValue(source);

            PropertyInfo targetProperty = target.GetType().GetProperty("Value");
            targetProperty.SetValue(target, source_string);

            return domainResource;
        }
    }
}
