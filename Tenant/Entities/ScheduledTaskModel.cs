using LantanaGroup.Link.Tenant.Jobs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using StackExchange.Redis;

namespace LantanaGroup.Link.Tenant.Entities
{
    public class ScheduledTaskModel
    {

        public string? KafkaTopic { get; set; } = string.Empty;
        public List<ReportTypeSchedule> ReportTypeSchedules  { get; set; } = new List<ReportTypeSchedule>();

        /* [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
         public Dictionary<string, string> EventData { get; set; }*/

        public class ReportTypeSchedule
        {
            public string? ReportType { get; set; } 
            public  List<string>  ScheduledTriggers { get; set; } = new List<string>();  
        }
    }

}
