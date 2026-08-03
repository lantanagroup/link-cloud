using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Tenant.Business.Models
{
    [DataContract]
    public class UpdateVendorModel
    {
        [Required]
        [DataMember]
        public string? Name { get; set; }
    }
}