namespace LantanaGroup.Link.Account.Application.Models.User
{
    public class GroupedUserModel
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public bool IsDeleted { get; set; } = false;
        public bool IsActive { get; set; } = false;

    }
}
