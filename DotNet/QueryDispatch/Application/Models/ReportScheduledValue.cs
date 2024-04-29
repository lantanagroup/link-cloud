using Newtonsoft.Json;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class ReportScheduledValue
    {
        public List<KeyValuePair<string, string>> Parameters { get; set; }

        public bool IsValid()
        {
            if (Parameters == null || Parameters.Count == 0)
            {
                return false;
            }

            return true;
        }
    }
}
