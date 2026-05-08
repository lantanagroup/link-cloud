using FluentValidation;
using LantanaGroup.Link.Account.Application.Models.Role;
using Link.Authorization.Permissions;

namespace LantanaGroup.Link.Account.Application.Validators
{
    public class RoleValidator : AbstractValidator<LinkRoleModel>
    {
        private readonly List<string> validClaims = LinkPermissionsProvider.GetLinkPermissions().ConvertAll(x => x.ToString());

        public RoleValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                    .WithMessage("Name is required");

            RuleForEach(x => x.Claims)
                .Must(ContainsValidClaims)
                .WithMessage("Claim '{PropertyValue}' is not a valid claim");
        }

        private bool ContainsValidClaims(string claim)
        {
            return validClaims.Contains(claim);
        }
    }
}
