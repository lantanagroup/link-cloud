namespace LantanaGroup.Link.Shared.Application.Utilities
{
    public static class ReportHelpers
    {
        public static string GetReportName(string scheduleID, string facilityId, List<string> reportTypes, DateTime? reportStartDate)
        {
            if (string.IsNullOrEmpty(scheduleID)) throw new ArgumentException("Schedule ID cannot be null or empty.", nameof(scheduleID));
            if (string.IsNullOrEmpty(facilityId)) throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
            if (reportTypes == null || reportTypes.Count == 0) throw new ArgumentException("Report types cannot be null or empty.", nameof(reportTypes));

            long hash = scheduleID.GetStableHashCode64();
            byte[] hashBytes = BitConverter.GetBytes(hash);
            string hashString = Convert.ToBase64String(hashBytes).TrimEnd('=').Replace("+", "-").Replace("/", "_");

            List<string> parts = [
                facilityId.ToLowerInvariant(),
                string.Join('+', reportTypes.Select(rt => MeasureNameShortener.ShortenMeasureName(rt).ToLowerInvariant()).Order())
                ];
            if (reportStartDate != null)
            {
                parts.Add(reportStartDate.Value.ToString("yyyyMMdd"));
            }
            parts.Add(hashString);
            return string.Join('_', parts);
        }
    }
}
