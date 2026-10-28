using FluentValidation;
using LantanaGroup.Link.Account.Application.Models;
using Link.Authorization.Permissions;

namespace LantanaGroup.Link.Account.Application.Validators
{
    public class ClaimValidator : AbstractValidator<LinkClaimsModel>
    {
        private readonly List<string> validClaims = LinkPermissionsProvider.GetLinkPermissions().ConvertAll(x => x.ToString());

        public ClaimValidator()
        {
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
