using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("SftpAcquisitionLog")]
public class SftpAcquisitionLog
{
    [Key]
    public long Id { get; set; }
    
    public Guid ExternalId { get; set; }
    
    public string FacilityId { get; set; } = string.Empty;
    
    public SftpAcquisitionType AcquisitionType { get; set; }

    public List<string> FileNames { get; set; } = [];

    /// <summary>
    /// When this acquisition should be processed. Null means process immediately.
    /// Logs are only picked up by the job when ScheduledDate is null or &lt;= DateTime.UtcNow.
    /// </summary>
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