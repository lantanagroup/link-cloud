namespace LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition
{
    public class DataAcquisitionQueryListConfigModel : BaseModel
    {
        public string FacilityId { get; set; }
        public string FhirServerBaseUrl { get; set; }
        public AuthenticationConfiguration Authentication { get; set; }
        public List<string> QueryPlanIds { get; set; } = new List<string>();

        public List<EhrPatientListModel> EHRPatientLists { get; set; } = new List<EhrPatientListModel>();

        public DataAcquisitionQueryListConfigModel() { }
    }

    public class EhrPatientListModel 
    {
        public List<string> ListIds { get; set; } = new List<string>();
        public List<string> MeasureIds { get; set; } = new List<string>();
    }
}
