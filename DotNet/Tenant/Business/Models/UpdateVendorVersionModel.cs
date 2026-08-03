using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Tenant.Business.Models
{
    [DataContract]
    public class UpdateVendorVersionModel
    {
        [Required]
        [DataMember]
        public string? Version { get; set; }
    }
}