using CronExpressionDescriptor;
using static LantanaGroup.Link.Tenant.Entities.ScheduledTaskModel;

namespace LantanaGroup.Link.Tenant.Models
{
    public class ScheduledTaskDto
    {
        public string? KafkaTopic { get; set; } = string.Empty;

        public List<ReportTypeDtoSchedule> ReportTypeSchedules { get; set; } = new List<ReportTypeDtoSchedule>();

        
        /* [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
         public Dictionary<string, string> EventData { get; set; }*/

        public class ReportTypeDtoSchedule
        {
            public string ReportType { get; set; }
            //public string ScheduledTrigger { get; set; }
            public List<string> ScheduledTriggers { get; set; } = new List<string>();

            public List<string>? ScheduledTriggerDescription
            {
                get
                {
                    var scheduledTriggersList = new List<string>();
                    foreach (string scheduledTrigger in ScheduledTriggers) 
                    {
                        scheduledTriggersList.Add(ExpressionDescriptor.GetDescription(scheduledTrigger));
                    }
                    return scheduledTriggersList;
                }

            }
        }
    }

}
