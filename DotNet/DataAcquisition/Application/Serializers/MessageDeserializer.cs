using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using Newtonsoft.Json;

namespace LantanaGroup.Link.DataAcquisition.Application.Serializers;

public class MessageDeserializer
{
    public static IBaseMessage DeserializeMessage(string topic, string rawMessage) =>
        topic switch
        {
            DataAcquisitionConstants.MessageNames.DataAcquisitionRequested => JsonConvert.DeserializeObject<DataAcquisitionRequested>(rawMessage),
            DataAcquisitionConstants.MessageNames.PatientCensusScheduled => new PatientCensusScheduled(),
            _ => throw new Exception($"{topic} not a valid topic. Unable to deserialize message.")
        };
}
