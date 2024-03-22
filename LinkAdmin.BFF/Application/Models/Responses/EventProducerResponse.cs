namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses
{
    public class EventProducerResponse
    {
        /// <summary>
        /// The Correlation Id of the event
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// The message about the event creation
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}
