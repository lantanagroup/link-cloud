using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class InitialQueryResult : IQueryPlan
{
    public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
}
