namespace LantanaGroup.Link.Account.Application.Models
{
    public class LinkClaimsModel
    {
        public LinkClaimsModel() : this([]) { }

        public LinkClaimsModel(List<string> claims)
        {
            Claims = claims;
        }

        /// <summary>
        /// A list of claims to assign to the user or role
        /// </summary>
        /// <example>["CanViewLogs"]</example>
        public List<string> Claims { get; set; }
    }
}
