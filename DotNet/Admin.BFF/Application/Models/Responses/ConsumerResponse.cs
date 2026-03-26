

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses
{
    public class ConsumerResponse
    {
       // declare a list of ConsumerResponseTopic
       public List<ConsumerResponseTopic> list { get; set; } = new List<ConsumerResponseTopic>();

    }

    public class ConsumerResponseTopic
    {
        public string topic { get; set; } = string.Empty;
        public string correlationId { get; set; } = string.Empty;
    }
}
