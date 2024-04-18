namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security
{
    public class Account
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public required string EmailAddress { get; set; }
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public List<string> FacilityIds { get; set; } = [];
        public List<Group> Groups { get; set; } = [];
        public List<Role> Roles { get; set; } = [];
    }
}
