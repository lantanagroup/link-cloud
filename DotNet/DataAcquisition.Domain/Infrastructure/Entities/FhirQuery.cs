using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("FhirQuery")]
public class FhirQuery
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FacilityId { get; set; }
    public FhirQueryType QueryType { get; set; }
    public List<Hl7.Fhir.Model.ResourceType> ResourceTypes { get; set; }
    public List<string> QueryParameters { get; set; } = new List<string>();
    public List<ResourceReferenceType> ResourceReferenceTypes { get; set; } = new List<ResourceReferenceType>();
    public int? Paged { get; set; }
    public string? MeasureId { get; set; }
    public bool? isReference { get; set; } = false;
    public DataAcquisitionLog DataAcquisitionLog { get; set; }
    public long DataAcquisitionLogId { get; set; }

    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }

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
