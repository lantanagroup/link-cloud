using FluentValidation;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Http;

namespace LantanaGroup.Link.Shared.Application.Filters
{
    public class ValidationFilter<T> : IEndpointFilter where T : class
    {
        private readonly IValidator<T> _validator;

        public ValidationFilter(IValidator<T> validator)
        {
            _validator = validator;
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var validatableObject = context.Arguments.OfType<T>().FirstOrDefault(t => t?.GetType() == typeof(T));

            if (validatableObject is not null)
            {
                var validationResult = _validator.Validate(validatableObject);

                if (validationResult.IsValid)
                {
                    return await next(context);
                }

                return Results.BadRequest(validationResult.Errors.ToResponse());
            }

            return await next(context);
        }
    }
}
