using FluentValidation;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses;
using System.Net;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Validation
{
    public class ValidationExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ValidationExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;                
                var messages = ex.Errors.Select(x => x.ErrorMessage).ToList();
                var validationFailureResposne = new ValidationFailureResponse
                {
                    Errors = messages
                };
               
                await context.Response.WriteAsJsonAsync(validationFailureResposne);
            }
        }
    }
}
