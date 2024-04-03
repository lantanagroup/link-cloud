using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{
    public class ReportScheduled : IReportScheduled
    {
        /// <summary>
        /// The facility id for the report
        /// </summary>
        /// <example>TestFacility01</example>
        public string FacilityId { get; set; } = string.Empty;

        /// <summary>
        /// The type of measure report to be generated
        /// </summary>
        /// <example>NHSNGlycemicControlHypoglycemicInitialPopulation</example>
        public string ReportType { get; set; } = string.Empty;

        /// <summary>
        /// The start date for the report period
        /// </summary>
        /// <example>2024-01-01</example>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// The end date for the report period
        /// </summary>
        /// <example>2024-01-31</example>
        public DateTime? EndDate { get; set; }
    }

    public class ReportScheduledMessage
    {
        public List<KeyValuePair<string, object>>? Parameters { get; set; }
    }

    public class ReportScheduledKey
    {
        public string? FacilityId { get; set; }
        public string? ReportType { get; set; }

        public static implicit operator string(ReportScheduledKey v)
        {
            throw new NotImplementedException();
        }
    }

    public class ScheduledReport
    {
        /// <summary>
        /// The type of measure report to be generated
        /// </summary>
        /// <example>NHSNGlycemicControlHypoglycemicInitialPopulation</example>
        public string ReportType { get; set; } = string.Empty;

        /// <summary>
        /// The start date for the reporting period
        /// </summary>
        /// <example>2024-01-01</example>
        public DateTime StartDate { get; set; } = DateTime.Now;

        /// <summary>
        /// The end date for the reporting period
        /// </summary>
        /// <example>2024-01-31</example>
        public DateTime EndDate { get; set; } = DateTime.Now;
    }
}
