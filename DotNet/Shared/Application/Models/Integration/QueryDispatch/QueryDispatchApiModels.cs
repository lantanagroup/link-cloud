namespace LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;

public class QueryDispatchConfigurationApiModel
{
    public string FacilityId { get; set; } = string.Empty;
    public List<DispatchScheduleApiModel> DispatchSchedules { get; set; } = [];
}

public class DispatchScheduleApiModel
{
    public string Event { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}
