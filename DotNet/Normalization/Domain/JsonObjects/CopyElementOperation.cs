namespace LantanaGroup.Link.Normalization.Domain.JsonObjects;

public class CopyElementOperation : INormalizationOperation
{
    public string FacilityId { get; set; }
    public string Name { get; set; }
    public string FromFhirPath { get; set; }
    public string ToFhirPath { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
