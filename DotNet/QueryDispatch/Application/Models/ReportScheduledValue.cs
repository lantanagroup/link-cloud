using Newtonsoft.Json;
using QueryDispatch.Application.Models;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class ReportScheduledValue
    {
        public List<string> ReportTypes { get; set; }
        public Frequency Frequency { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }

        public bool IsValid()
        {
            if (ReportTypes == null || ReportTypes.Count <= 0 || StartDate == default || EndDate == default)
            {
                return false;
            }

            return true;
        }
    }
}