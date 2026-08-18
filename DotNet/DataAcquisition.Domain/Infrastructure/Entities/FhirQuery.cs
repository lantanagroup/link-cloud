using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
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

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }

    public int? Paged { get; set; }

    [Required]
    public List<string> QueryParameters { get; set; } = new List<string>();

    [Required]
    public FhirQueryType? QueryType { get; set; } = 0;

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
            const string prefix = "_id=";
            var ids = (value ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            var withoutId = (QueryParameters ?? [])
                .Where(p => !p.StartsWith(prefix))
                .ToList();

            // Only re-append an "_id=..." entry when we actually have ids to write;
            // an empty assignment must not leave a stray "_id=" in QueryParameters.
            if (ids.Count > 0)
                withoutId.Add($"{prefix}{string.Join(',', ids)}");

            QueryParameters = withoutId;
        }
    }
}