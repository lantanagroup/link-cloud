using Hl7.Fhir.Model;

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
            CopyPropertyOperationHelper.CopyFhirPathValue(domainResource, SourceFhirPath, TargetFhirPath);

            return domainResource;
        }
    }
}
