namespace LantanaGroup.Link.Account.Application.Models
{
    public class UserManagementConfig
    {
        public bool EnableAutomaticUserActivation { get; set; } = true;
        public bool SoftDeleteUsers { get; set; } = true;
    }
}
