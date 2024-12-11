namespace LantanaGroup.Link.Account.Application.Models.Role
{
    public class LinkRoleModel
    {
        public LinkRoleModel() : this(Guid.Empty, string.Empty, string.Empty, []) { }

        public LinkRoleModel(Guid id, string name, string description, List<string> claims)
        {
            Id = id;
            Name = name;
            Description = description;
            Claims = claims;
        }

        /// <summary>
        /// The id of the role
        /// </summary>
        /// <example>627c8762-de3c-4c92-a9f5-25483b6e7922</example>
        public Guid Id { get; set; }

        /// <summary>
        /// The name of the role
        /// </summary>
        /// <example>LinkAdministrator</example>
        public string Name { get; set; }

        /// <summary>
        /// The description of the role
        /// </summary>
        /// <example>Administrator for the Link application</example>
        public string Description { get; set; }

        /// <summary>
        /// The claims associated with the role
        /// </summary>
        /// <example>["CanViewLogs", "CanViewResources"]</example>
        public List<string> Claims { get; set; }
    }
}
