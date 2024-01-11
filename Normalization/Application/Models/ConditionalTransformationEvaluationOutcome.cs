namespace LantanaGroup.Link.Normalization.Application.Models;

public class ConditionalTransformationEvaluationOutcome
{
    public bool AllConditionsMet { get; set; } = false;
    public List<ConditionEvaluationOutcome> ConditionEvaluationOutcomes { get; set; } = new List<ConditionEvaluationOutcome>();
}
