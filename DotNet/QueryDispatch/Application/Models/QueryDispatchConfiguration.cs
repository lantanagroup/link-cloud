using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models;

[DataContract]
public class QueryDispatchConfiguration
{
    [DataMember]
    public string? FacilityId { get; set; }
    [DataMember]
    public List<DispatchSchedule> DispatchSchedules { get; set; }

}
