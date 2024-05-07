using FluentValidation;
using LantanaGroup.Link.Account.Application.Models.Role;

namespace LantanaGroup.Link.Account.Application.Validators
{
    public class RoleValidator : AbstractValidator<LinkRoleModel>
    {
        public RoleValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                    .WithMessage("Name is required");
        }
    }
}
