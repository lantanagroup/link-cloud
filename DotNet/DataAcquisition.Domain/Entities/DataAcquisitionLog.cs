using DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAcquisition.Domain.Entities;

[Table("DataAcquisitionLog")]
public class DataAcquisitionLog : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public AcquisitionPriority Priority { get; set; }
    public string PatientId { get; set; }
    public string FhirVersion { get; set; }
    public FhirQueryType QueryType { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public ICollection<FhirQuery> FhirQuery { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime ExecutionDate { get; set; }
    public string TimeZone { get; set; }
    public int RetryAttempts { get; set; } = 0;
    public DateTime? CompletionDate { get; set; }
    public long? CompletionTimeMilliseconds { get; set; }
    public List<string> ResourceAcquiredIds { get; set; } = new List<string>();
    public ICollection<ReferenceResources> ReferenceResources { get; set; }
    public List<string> Notes { get; set; } = new List<string>();
    public ScheduledReport ScheduledReport { get; set; }
}
