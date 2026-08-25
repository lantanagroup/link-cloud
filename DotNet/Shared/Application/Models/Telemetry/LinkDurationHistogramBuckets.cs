namespace LantanaGroup.Link.Shared.Application.Models.Telemetry
{
    /// <summary>
    /// Explicit histogram buckets for stage-duration instruments (milliseconds).
    /// Default OTel buckets top out near 10 s; EHR Observation queries are measured at ~23 s.
    /// </summary>
    public static class LinkDurationHistogramBuckets
    {
        public static readonly double[] Milliseconds =
        [
            1, 2, 5, 10, 25, 50, 100, 250, 500,
            1000, 2500, 5000, 10000, 15000, 30000, 45000, 60000
        ];

        /// <summary>
        /// .NET duration histogram instrument names that must use <see cref="Milliseconds"/>.
        /// Java duration histograms set the same boundaries via advice at construction.
        /// </summary>
        public static readonly string[] InstrumentNames =
        [
            DiagnosticNames.DataAcquisitionQueryDuration,
            DiagnosticNames.NormalizationDuration,
            "link_submission_upload_duration"
        ];
    }
}
