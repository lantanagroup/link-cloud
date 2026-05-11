using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog
{
    public class UpdateDataAcquisitionLogModel
    {
        [Required, DataMember]
        public required long? Id { get; set; }
        public RequestStatus? Status { get; set; }
        public int? RetryAttempts { get; set; }

        /// <summary>
        /// Notes to append to the log. These are added to the existing notes,
        /// never replacing them. Pass null or empty to skip note insertion.
        /// </summary>
        public List<string>? NewNotes { get; set; }
        public DateTime? ExecutionDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public long? CompletionTimeMilliseconds { get; set; }
        public string? TraceId { get; set; }
        public List<string>? ResourceAcquiredIds { get; set; }
    }
}
