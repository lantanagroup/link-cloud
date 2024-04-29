namespace LantanaGroup.Link.Normalization.Domain.JsonObjects;

public class ConceptMapOperation : INormalizationOperation
{
    public string FacilityId { get; set; }
    public string Name { get; set; }
    public object? FhirConceptMap { get; set; }
    public string FhirPath { get; set; }
    public string FhirContext { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
