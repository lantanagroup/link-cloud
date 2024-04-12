using LantanaGroup.Link.Audit.Application.Interfaces;

namespace LantanaGroup.Link.Audit.Application.Models
{
    public class PagedAuditModel : IPagedModel<AuditModel>
    {
        public List<AuditModel> Records { get; set; } = new List<AuditModel>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedAuditModel() { }

        public PagedAuditModel(List<AuditModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
