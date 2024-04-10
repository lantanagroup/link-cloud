using LantanaGroup.Link.Account.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Account.Domain.Entities
{

    [Table("Accounts")]
    public class AccountModel : BaseEntity
    {

        public string Username { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public List<string>? FacilityIds { get; set; }

        public ICollection<GroupModel> Groups { get; set; } = new List<GroupModel>();
        public ICollection<RoleModel> Roles { get; set; } = new List<RoleModel>();

        public DateTime? LastSeen { get; set; }


    }
}
