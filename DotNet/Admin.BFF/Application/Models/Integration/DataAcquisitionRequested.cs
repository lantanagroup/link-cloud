using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{
    public class DataAcquisitionRequested : IDataAcquisitionRequested
    {
        /// <summary>
        /// The key for the data acquisition request (facility id)
        /// </summary>
        /// <example>TestFacility01</example>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The patient id for the data acquisition request
        /// </summary>
        /// <example>TestPatient01</example>
        public string PatientId { get; set; } = string.Empty;

        /// <summary>
        /// The query type for the data acquisition request. Valid values are Initial and Supplemental
        /// </summary>
        /// <example>Initial</example>
        public string QueryType { get; set; } = string.Empty;

        /// <summary>
        /// The scheduled reports for the facility, used to generate the requirements for data acquisition
        /// </summary>
        public List<ScheduledReport> ScheduledReports { get; set; } = [];
    }

    public class DataAcquisitionRequestedMessage
    {
        public string PatientId { get; set; } = string.Empty;
        public string QueryType { get; set; } = string.Empty;
        public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
    }
}
