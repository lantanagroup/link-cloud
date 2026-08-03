using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Tenant.Business.Models
{
    [DataContract]
    public class CreateVendorVersionModel
    {
        [Required]
        [DataMember]
        public Guid? VendorId { get; set; }

        [Required]
        [DataMember]
        public string? Version { get; set; }
    }
}