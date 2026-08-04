using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Tenant.Data.Entities;

namespace LantanaGroup.Link.Tenant.Entities;

[Table("Facilities", Schema = "dbo")]
public class Facility
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string FacilityId { get; set; } = "";

    public string? FacilityName { get; set; }

    [Required]
    public string TimeZone { get; set; } = "";

    public bool IsDeleted { get; set; } 

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }

    public Guid? VendorVersionId {get;set;}
    public VendorVersion? VendorVersion { get; set; }

    [Required]
    public ScheduledReportModel ScheduledReports { get; set; } = null!;

    public Facility ShallowCopy()
    {
        return (Facility)this.MemberwiseClone();
    }
}