using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog
{
    public class UpdateDataAcquisitionLogModel
    {
        [Required, DataMember]
        public required long Id { get; set; }
        public RequestStatus? Status { get; set; }
        public int? RetryAttempts { get; set; }
        public List<string>? Notes { get; set; }
        public DateTime? ExecutionDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public long? CompletionTimeMilliseconds { get; set; }
        public string? TraceId { get; set; }
        public List<string>? ResourceAcquiredIds { get; set; }
    }
}
