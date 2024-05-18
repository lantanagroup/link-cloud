using FluentValidation;
using LantanaGroup.Link.Account.Application.Models.User;
using Link.Authorization.Permissions;

namespace LantanaGroup.Link.Account.Application.Validators
{
    public class UserValidator : AbstractValidator<LinkUserModel>
    {
        private readonly List<string> validClaims = LinkPermissionsProvider.GetLinkPermissions().ConvertAll(x => x.ToString());

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

            RuleForEach(x => x.UserClaims)
                .Must(ContainsValidClaims)
                .WithMessage("Claim '{PropertyValue}' is not a valid claim");
        }

        private bool ContainsValidClaims(string claim)
        {
            return validClaims.Contains(claim);
        }

    }
}
