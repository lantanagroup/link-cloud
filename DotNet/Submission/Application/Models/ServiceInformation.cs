namespace LantanaGroup.Link.Submission.Application.Models
{ 
    /// <summary>
    /// Information about the API service
    /// </summary>
    public class ServiceInformation
    {
        /// <summary>
        /// The name of the service
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The version of the service
        /// </summary>
        public string Version { get; set; } = string.Empty;
    }
}
