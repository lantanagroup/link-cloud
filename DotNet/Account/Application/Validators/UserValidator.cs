using FluentValidation;
using LantanaGroup.Link.Account.Application.Models.User;

namespace LantanaGroup.Link.Account.Application.Validators
{
    public class UserValidator : AbstractValidator<LinkUserModel>
    {
        public UserValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty()
                    .WithMessage("First name is required");

            RuleFor(x => x.LastName)
                .NotEmpty()
                    .WithMessage("Last name is required");

            RuleFor(x => x.Email)
                .NotEmpty()
                    .WithMessage("Email is required")
                .EmailAddress()
                    .WithMessage("Email is not valid");           
        }
    }
}
