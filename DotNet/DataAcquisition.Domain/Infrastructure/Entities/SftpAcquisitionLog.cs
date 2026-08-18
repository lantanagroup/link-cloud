using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("SftpAcquisitionLog")]
public class SftpAcquisitionLog
{
    [Key]
    public long Id { get; set; }

    public Guid ExternalId { get; set; }

    public string FacilityId { get; set; } = string.Empty;

    public SftpAcquisitionType AcquisitionType { get; set; }

    public SftpAcquisitionSubType SubType { get; set; } = SftpAcquisitionSubType.None;

    public List<string> FileNames { get; set; } = [];

    public DateTime? ScheduledDate { get; set; }

    public DateTime? ProcessDate { get; set; }

    public int? RetryAttempts { get; set; }

    public RequestStatus Status { get; set; }

    [StringLength(32)]
    public string? OriginatingTraceId { get; set; }

    [StringLength(16)]
    public string? OriginatingSpanId { get; set; }

    public List<string> Notes { get; set; } = [];

    public List<SftpAcquisitionBenchmark>? Benchmarks { get; set; }
}