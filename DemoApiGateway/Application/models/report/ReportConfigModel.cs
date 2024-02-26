namespace LantanaGroup.Link.DemoApiGateway.Application.models
{
    public class ReportConfigModel
    {
        public string Id { get; set; } = string.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public string BundlingType { get; set; }
    }

}
