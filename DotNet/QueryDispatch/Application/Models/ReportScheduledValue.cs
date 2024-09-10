using Newtonsoft.Json;
using QueryDispatch.Application.Models;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class ReportScheduledValue
    {
        public List<string> ReportType { get; set; }
        public Frequency Frequency { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }

        public bool IsValid()
        {
            if (ReportType == null || ReportType.Count <= 0 || StartDate == default || EndDate == default)
            {
                return false;
            }

            return true;
        }
    }
}