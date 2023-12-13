using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Attributes;

namespace LantanaGroup.Link.Validation.Entities
{
    [BsonCollection("validation")]
    public class ValidationEntity : BaseEntity
    {
        //TODO: Add in more relevant data pertaining to tenant level configuration as needed
        public string? TenantId { get; set; }
        public string? PatientId { get; set; }
        public bool? IsValid { get; set; }
        
    }
}
