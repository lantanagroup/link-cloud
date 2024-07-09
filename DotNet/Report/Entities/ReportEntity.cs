using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Report.Entities
{

    public class ReportEntity : BaseEntity
    {
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}