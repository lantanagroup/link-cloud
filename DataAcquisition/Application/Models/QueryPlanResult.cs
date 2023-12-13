using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class QueryPlanResult : IQueryPlan
{
    public QueryPlan QueryPlan { get; set; }
}
