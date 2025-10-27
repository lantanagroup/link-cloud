using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;

namespace DataAcquisition.Domain.Application.Models
{
    public class CreateDataAcquisitionLogModel
    {
        public string FacilityId { get; set; }
        public AcquisitionPriority Priority { get; set; }
        public string? PatientId { get; set; }
        public string? CorrelationId { get; set; }
        public string? FhirVersion { get; set; }
        public bool IsCensus { get; set; }
        public ReportableEvent? ReportableEvent { get; set; }
        public FhirQueryType? QueryType { get; set; }
        public QueryPhase QueryPhase { get; set; }
        public List<CreateFhirQueryModel> FhirQuery { get; set; } = new List<CreateFhirQueryModel>();
        public RequestStatus Status { get; set; }
        public DateTime? ExecutionDate { get; set; }
        public string? TraceId { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
        public required ScheduledReport ScheduledReport { get; set; }
        public string ResourceId { get; internal set; }
    }
}
