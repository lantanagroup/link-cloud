using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("FhirQuery")]
[Index("DataAcquisitionLogId", Name = "IX_FhirQuery_DataAcquisitionLogId")]
public partial class FhirQuery
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string FacilityId { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }

    public int? Paged { get; set; }

    [Required]
    public List<string> QueryParameters { get; set; } = new List<string>();

    [Required]
    public FhirQueryType QueryType { get; set; }

    public string? MeasureId { get; set; }
    public TimeFrame? CensusTimeFrame { get; set; } = null;
    public ListType? CensusPatientStatus { get; set; } = null;
    public string? CensusListId { get; set; } = null;

    [Column("isReference")]
    public bool? IsReference { get; set; } = false;

    public long DataAcquisitionLogId { get; set; }

    [ForeignKey("DataAcquisitionLogId")]
    [InverseProperty("FhirQueries")]
    public virtual DataAcquisitionLog DataAcquisitionLog { get; set; }

    [InverseProperty("FhirQuery")]
    public virtual ICollection<ResourceReferenceType> ResourceReferenceTypes { get; set; } = new List<ResourceReferenceType>();

    [InverseProperty("FhirQuery")]
    public virtual ICollection<FhirQueryResourceType> FhirQueryResourceTypes { get; set; } = new List<FhirQueryResourceType>();

    [NotMapped]
    public IEnumerable<string> IdQueryParameterValues
    {
        get
        {
            string prefix = "_id=";
            return (QueryParameters ?? []).Where(p => p.StartsWith(prefix))
                .Select(p => p.Substring(prefix.Length))
                .SelectMany(p => p.Split(','))
                .Where(id => id != "");
        }
        set
        {
            string prefix = "_id=";
            QueryParameters = (QueryParameters ?? []).Where(p => !p.StartsWith(prefix))
                .Append($"{prefix}{string.Join(',', (value ?? []))}")
                .ToList();
        }
    }
}