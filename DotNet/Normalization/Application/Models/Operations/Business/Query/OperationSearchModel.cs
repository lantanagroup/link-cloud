using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Shared.Application.Enums;
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query
{
    [ExcludeFromCodeCoverage]
    public class OperationSearchModel
    {
        public Guid? OperationId { get; set; }
        public OperationType? OperationType { get; set; }
        public string? FacilityId { get; set; }
        public bool IncludeDisabled { get; set; }
        public string? ResourceType { get; set; }
        public string? SortBy { get; set; }
        public SortOrder? SortOrder { get; set; }
        public int? PageSize { get; set; }
        public int? PageNumber { get; set; }
    }
}
