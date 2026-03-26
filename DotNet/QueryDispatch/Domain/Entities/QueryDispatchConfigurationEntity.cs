using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    [Table("queryDispatchConfigurations")]
    public class QueryDispatchConfigurationEntity : BaseQueryEntity 
    {
        public List<DispatchSchedule> DispatchSchedules { get; set; }
    }
}
