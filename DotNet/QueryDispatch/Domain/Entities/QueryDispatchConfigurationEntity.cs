namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    [BsonCollection("queryDispatchConfigurations")]
    public class QueryDispatchConfigurationEntity : BaseQueryEntity 
    {
        public List<DispatchSchedule> DispatchSchedules { get; set; }
    }
}
