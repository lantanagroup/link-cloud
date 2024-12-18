using AngleSharp.Css.Values;
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
        /// The frequency to generate the report
        /// </summary>
        public Frequency Frequency { get; set; }

        /// <summary>
        /// The type of measure report to be generated
        /// </summary>
        public List<string> ReportTypes { get; set; } = new List<string>();

        /// <summary>
        /// The start date for the report period
        /// </summary>
        /// <example>2024-01-01T00:00:00Z</example>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// The Delay for the report period
        /// </summary>
        public string Delay { get; set; } = string.Empty;
  
    }

    public class ReportScheduledMessage
    {
        public List<string> ReportTypes { get; set; } = new List<string>();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Frequency { get; set;}
    }

    public class ReportScheduledKey
    {
        public string? FacilityId { get; set; }

    }

    public class ScheduledReport
    {
        /// <summary>
        /// The type of measure report to be generated
        /// </summary>
        /// <example>NHSNdQMAcuteCareHospitalInitialPopulation</example>
        public List<string> ReportTypes { get; set; } = new List<string>();

        /// <summary>
        /// The start date for the reporting period
        /// </summary>
        /// <example>2024-01-01T00:00:00Z</example>
        public DateTime StartDate { get; set; } = DateTime.Now;

        /// <summary>
        /// The end date for the reporting period
        /// </summary>
        /// <example>2024-01-31T23:59:59Z</example>
        public DateTime EndDate { get; set; } = DateTime.Now;
    }
}
