using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;

namespace LantanaGroup.Link.Normalization.Application.Services;

public interface IConditionalTransformationEvaluationService
{
    ConditionalTransformationEvaluationOutcome Evaluate(ConditionalTransformationOperation operation, Resource resource);
    //Task<ConditionalTransformationEvaluationOutcome> EvaluateAsync(ConditionalTransformationOperation operation, Bundle bundle, CancellationToken cancellationToken = default);
}
