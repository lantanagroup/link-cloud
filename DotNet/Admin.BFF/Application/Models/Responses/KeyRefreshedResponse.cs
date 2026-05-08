namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses
{
    public class KeyRefreshedResponse
    {
        /// <summary>
        /// The message about the outcome of the operation.
        /// </summary>
        /// <example>The signing key for link bearer services was refreshed successfully.</example>
        public string Message { get; init; } = string.Empty;
    }
}
