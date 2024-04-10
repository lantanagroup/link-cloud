using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.audit
{
    public class PagedAuditModel : IPagedModel<AuditModel>
    {
        public List<AuditModel> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }

        public PagedAuditModel() { }

        public PagedAuditModel(List<AuditModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
