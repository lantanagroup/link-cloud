using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class UpdateVendorModel
    {
        [Required]
        [DataMember]
        public string? Name { get; set; }
    }
}