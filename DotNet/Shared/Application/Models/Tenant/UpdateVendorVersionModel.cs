using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class UpdateVendorVersionModel
    {
        [Required]
        [DataMember]
        public string? Version { get; set; }
    }
}