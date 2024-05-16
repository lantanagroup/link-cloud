using FluentValidation.Results;

namespace LantanaGroup.Link.Shared.Application.Models.Responses
{
    public class ValidationFailureResponse
    {
        /// <summary>
        /// A list of validation errors
        /// </summary>
        /// <example>"PatientId is required"</example>
        public List<string> Errors { get; init; } = [];
    }

    public static class ValidationFailureMapper
    {
        public static ValidationFailureResponse ToResponse(this IEnumerable<ValidationFailure> errors)
        {
            var messages = errors.Select(x => x.ErrorMessage).ToList();
            return new ValidationFailureResponse
            {
                Errors = messages
            };
        }
    }
}
