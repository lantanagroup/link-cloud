namespace LantanaGroup.Link.Normalization.Application.Models;

public class ConditionalTransformationEvaluationOutcome
{
    public bool AllConditionsMet { get; set; } = true;
    public List<ConditionEvaluationOutcome> ConditionEvaluationOutcomes { get; set; } = new List<ConditionEvaluationOutcome>();
}
