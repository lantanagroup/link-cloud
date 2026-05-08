using LantanaGroup.Link.QueryDispatch;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using QueryDispatch.Application.Settings;

namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    //TODO: Daniel - This is used in Entities and Models... See where this best resides.
    public class DispatchSchedule
    {
        [BsonRepresentation(BsonType.String)]
        public QueryDispatchConstants.EventType Event { get; set; }
        
        /// <summary>
        /// The ISO-8601 formatted trigger time to generate a DataAcquisitionRequested event.
        /// </summary>
        public string Duration { get; set; }
    }
}
