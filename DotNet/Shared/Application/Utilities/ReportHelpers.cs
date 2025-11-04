namespace LantanaGroup.Link.Shared.Application.Utilities
{
    public static class ReportHelpers
    {
        public static string GetReportName(string scheduleID, string facilityId, List<string> reportTypes, DateTime? reportStartDate)
        {
            if (string.IsNullOrEmpty(scheduleID)) throw new ArgumentException("Schedule ID cannot be null or empty.", nameof(scheduleID));
            if (string.IsNullOrEmpty(facilityId)) throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
            if (reportTypes == null || reportTypes.Count == 0) throw new ArgumentException("Report types cannot be null or empty.", nameof(reportTypes));

            // Sanitize scheduleID to make it URL-safe
            string sanitizedScheduleID = scheduleID.ToLowerInvariant()
                .Replace("/", "_")   // Replace / with _
                .Replace("+", "-")   // Replace + with -
                .Replace("=", string.Empty); // Remove = (trim padding)

            List<string> parts = [
                facilityId.ToLowerInvariant(),
        string.Join('+', reportTypes.Select(rt => MeasureNameShortener.ShortenMeasureName(rt).ToLowerInvariant()).Order())
            ];
            if (reportStartDate != null)
            {
                parts.Add(reportStartDate.Value.ToString("yyyyMMdd"));
            }
            parts.Add(sanitizedScheduleID);
            return string.Join('_', parts);
        }
    }
}
