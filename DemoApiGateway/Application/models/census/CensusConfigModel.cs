namespace LantanaGroup.Link.DemoApiGateway.Application.models.census
{
    public class CensusConfigModel
    {      
        public string FacilityId { get; set; }
        public string ScheduledTrigger { get; set; }

        public CensusConfigModel() { }

        public CensusConfigModel(string facilityId, string scheduledTrigger)
        {        
            FacilityId = facilityId;
            ScheduledTrigger = scheduledTrigger;
        }        
    }
}
