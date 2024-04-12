using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class SupplementalQueryResult : IQueryPlan
{
    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }
}
