namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class ReportScheduledKey
    {
        public string FacilityId { get; set; }
        public string ReportType { get; set; }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(FacilityId) || string.IsNullOrWhiteSpace(ReportType))
            {
                return false;
            }

            return true;
        }
    }
}
