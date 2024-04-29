using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;

namespace LantanaGroup.Link.Normalization.Application.Services;

public class ConditionalTransformationEvaluationService : IConditionalTransformationEvaluationService
{
    public ConditionalTransformationEvaluationOutcome Evaluate(ConditionalTransformationOperation operation, Resource resource)
    {
        var outcome = new ConditionalTransformationEvaluationOutcome();
        var conditions = operation.Conditions;
        var element = resource.Select(operation.TransformElement).FirstOrDefault();

        if(element == null)
        {
            outcome.AllConditionsMet = false;
            return outcome;
        }

        foreach(var condition in conditions)
        {
            var elementToEvaluate = resource.Select(condition.Element).FirstOrDefault();
            
            if(elementToEvaluate == null)
            {
                outcome.AllConditionsMet = false;
                outcome.ConditionEvaluationOutcomes.Add(new ConditionEvaluationOutcome
                {
                    ConditionElement = condition,
                    ConditionMet = false
                });
                return outcome;
            }

            var result = condition.Operator switch
            {
                Operators.Equal => elementToEvaluate.ToString().Equals(condition.OperatorValue, StringComparison.OrdinalIgnoreCase),
                Operators.NotEqual => !elementToEvaluate.ToString().Equals(condition.OperatorValue, StringComparison.OrdinalIgnoreCase),
                Operators.GreaterThan => elementToEvaluate.ToString().CompareTo(condition.OperatorValue) > 0,
                Operators.GreaterThanOrEqual => elementToEvaluate.ToString().CompareTo(condition.OperatorValue) >= 0,
                Operators.LessThan => elementToEvaluate.ToString().CompareTo(condition.OperatorValue) < 0,
                Operators.LessThanOrEqual => elementToEvaluate.ToString().CompareTo(condition.OperatorValue) <= 0,
                Operators.Exists => elementToEvaluate != null,
                Operators.NotExists => elementToEvaluate == null,
                _ => throw new BadOperationException()
            };

            outcome.ConditionEvaluationOutcomes.Add(new ConditionEvaluationOutcome
            {
                ConditionElement = condition,
                ConditionMet = result
            });
        }

        outcome.AllConditionsMet = outcome.ConditionEvaluationOutcomes.All(x => x.ConditionMet);

        return outcome;
    }
}
