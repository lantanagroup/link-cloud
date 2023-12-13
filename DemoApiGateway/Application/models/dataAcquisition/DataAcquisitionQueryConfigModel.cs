namespace LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition
{
    public class DataAcquisitionQueryConfigModel
    {
        public string FacilityId { get; set; }
        public string FhirServerBaseUrl { get; set; }
        public AuthenticationConfiguration Authentication { get; set; }
        public List<string> QueryPlanIds { get; set; }
        public DataAcquisitionQueryConfigModel() { }
    }
}
