namespace LantanaGroup.Link.Report.Application.Options
{
    public class PreQualificationSettings
    {
        /// <summary>
        /// The key as stored in Azure App Configuration, whose convention is '/'-separated (see the
        /// existing '/link/report/base-url'). Spring Cloud Azure maps '/' to '.' when it builds the
        /// property name, so the Java Validation service sees this same row as
        /// 'pre-qualification.write-pre-qual-operation-outcome'. The .NET provider passes keys through
        /// verbatim, so Report has to ask for the slashed string to read that one row.
        /// </summary>
        public const string AppConfigurationKey = "/pre-qualification/write-pre-qual-operation-outcome";

        /// <summary>
        /// The key as it appears in appsettings.json and as a docker-compose environment variable,
        /// where a leading-slash name is not usable. Deliberately the Spring dotted spelling so the
        /// local stack sets one variable for both runtimes. A dot is not a section separator in .NET
        /// configuration (only ':' is), so this binds as a flat key — which is why this setting is
        /// read directly rather than through GetSection like the others in this service.
        /// </summary>
        public const string WritePreQualOperationOutcomeKey = "pre-qualification.write-pre-qual-operation-outcome";

        /// <summary>
        /// When true, the Validation service is the sole writer of the pre-qualification
        /// OperationOutcome to the patient NDJSON (LEGLINK-425) and Report skips its own
        /// legacy "Patient has failed Validation" append. When false, Report retains that write.
        /// Both runtimes must be set to the same value.
        /// </summary>
        public bool WritePreQualOperationOutcome { get; set; }
    }
}
