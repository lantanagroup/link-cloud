namespace LantanaGroup.Link.Account.Application.Models.Role
{
    public class RoleClaimsModel
    {
        public RoleClaimsModel() : this([]) { }

        public RoleClaimsModel(List<string> claims)
        {
            Claims = claims;
        }

        public List<string> Claims { get; set; }
    }
}
