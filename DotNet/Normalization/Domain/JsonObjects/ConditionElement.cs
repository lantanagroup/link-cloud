namespace LantanaGroup.Link.Normalization.Domain.JsonObjects;

public class ConditionElement
{
    public string FacilityId { get; set; }
    public string Name { get; set; }
    public string Element { get; set; }
    public Operators Operator { get; set; }
    public string OperatorValue { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}

public enum Operators
{
    Equal, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, NotEqual, Exists, NotExists
}