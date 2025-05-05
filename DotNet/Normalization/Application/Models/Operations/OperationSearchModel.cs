using LantanaGroup.Link.Normalization.Application.Operations;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations
{
    public class OperationSearchModel
    {
        public Guid? Id { get; set; }
        public OperationType? OperationType { get; set; }
        public string? FacilityId { get; set; }
        public bool IncludeDisabled { get; set; }
    }
}
