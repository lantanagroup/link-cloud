namespace LantanaGroup.Link.Normalization.Domain.JsonObjects;
public class ConditionalTransformationOperation : INormalizationOperation
{
    public string FacilityId { get; set; }
    public string Name { get; set; }
    public List<ConditionElement> Conditions { get; set; }
    public string TransformResource { get; set; }
    public string TransformElement { get; set; }
    public string TransformValue { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
