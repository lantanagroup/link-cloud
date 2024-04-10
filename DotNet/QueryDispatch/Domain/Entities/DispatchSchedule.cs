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
        /// The duration of time before a DataAcquisitionRequested event is triggered.
        /// </summary>
        public int Duration { get; set; }

        [BsonRepresentation(BsonType.String)]
        public QueryDispatchConstants.DurationType DurationType { get; set; }
    }
}
