namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses
{
    public class EventProducerResponse
    {
        /// <summary>
        /// The Correlation Id of the event
        /// </summary>
        /// <example>fad42853-ac1f-45ad-94ae-f858b1d41945</example>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// The message about the event creation
        /// </summary>
        /// <example>The patient event was created succcessfully with a correlation id of 'fad42853-ac1f-45ad-94ae-f858b1d41945'.</example>
        public string Message { get; init; } = string.Empty;
    }
}
