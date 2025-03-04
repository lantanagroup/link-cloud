namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security
{
    public class Account
    {
        public required string Id { get; set; }
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = default!;
        public List<string> UserClaims { get; set; } = [];
        public List<string> RoleClaims { get; set; } = [];
        public List<string> Roles { get; set; } = [];
    }
}
