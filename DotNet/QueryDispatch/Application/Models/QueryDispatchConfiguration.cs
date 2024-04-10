using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class QueryDispatchConfiguration
    {
        public string FacilityId { get; set; }
        public List<DispatchSchedule> DispatchSchedules { get; set; }

    }
}
