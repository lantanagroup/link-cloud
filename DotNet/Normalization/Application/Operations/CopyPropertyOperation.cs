using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Operations.Helpers;
using System.Reflection;

namespace LantanaGroup.Link.Normalization.Application.Operations
{

    public class CopyPropertyOperation : IOperation
    {
        public OperationType OperationType => OperationType.CopyProperty;
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
            var source = domainResource.Select(SourceFhirPath).FirstOrDefault();
            var target = PropertyHelper.GetProperty(domainResource, TargetFhirPath);

            PropertyInfo propertyInfo = typeof(FhirString).GetProperty("Value");
            string source_string = (string)propertyInfo.GetValue(source);

            PropertyInfo targetProperty = target.GetType().GetProperty("Value");
            targetProperty.SetValue(target, source_string);

            return domainResource;
        }
    }
}
