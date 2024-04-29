using LantanaGroup.Link.Normalization.Domain.JsonObjects;

namespace LantanaGroup.Link.Normalization.Application.Models;

public class ConditionEvaluationOutcome
{
    public string Element { get; set; }
    public string ElementValue { get; set; }
    public ConditionElement ConditionElement { get; set; }
    public bool ConditionMet { get; set; }
}