namespace LantanaGroup.Link.Account.Application.Models
{
    public class LinkUserModel
    {
        public LinkUserModel() : this(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, [], [], []) { }
        
        public LinkUserModel(string id, string userName, string firstName, string? middleName, string lastName, string email, List<string> roles, List<string> claims, List<string> facilities)
        {
            Id = id;
            UserName = userName;
            FirstName = firstName;
            MiddleName = middleName;
            LastName = lastName;
            Email = email;
            Roles = roles;
            Claims = claims;
            Facilities = facilities;
        }

        /// <summary>
        /// The unique identifier for the user
        /// </summary>
        /// <example>5d7096b5-aa51-4f29-a840-6df98aaa9356</example>
        public string Id { get; set; }

        /// <summary>
        /// The first name of the user
        /// </summary>
        /// <example>John</example>
        public string FirstName { get; set; }

        /// <summary>
        /// The middle name of the user
        /// </summary>
        /// <example>Diddely</example>
        public string? MiddleName { get; set; }

        /// <summary>
        /// The last name of the user
        /// </summary>
        /// <example>Doe</example>
        public string LastName { get; set; }

        /// <summary>
        /// The username of the user
        /// </summary>
        /// <example>john.doe</example>
        public string UserName { get; set; }

        /// <summary>
        /// The email address of the user
        /// </summary>
        /// <example>john.doe@gmail.com</example>
        public string Email { get; set; }

        /// <summary>
        /// The roles assigned to the user
        /// </summary>
        /// <example>["Admin", "User"]</example>
        public List<string> Roles { get; set; }

        /// <summary>
        /// The claims assigned to the user
        /// </summary>
        /// <example>["CanViewLogs"]</example>
        public List<string> Claims { get; set; }

        /// <summary>
        /// The facilities the user has access to
        /// </summary>
        /// <example>["TestFacility01"]</example>
        public List<string> Facilities { get; set; }        
    }
}
