namespace LantanaGroup.Link.Report.Application.Options
{
    public class PreQualificationSettings
    {
        public const string Key = "PreQualification";

        /// <summary>
        /// The Java Validation service's counterpart key, in Spring's dotted spelling. Not read by
        /// .NET — it is declared here so the correspondence between the two spellings is asserted by
        /// tests rather than assumed, and so the pairing is discoverable from this class.
        /// <para>
        /// The two runtimes deliberately keep separate keys: Spring cannot bind a colon-separated key,
        /// and the Java services are being retired, so a shared key would leave the surviving .NET
        /// services carrying a translation layer for a platform that no longer exists. In Azure App
        /// Configuration these are two rows — 'PreQualification:WritePreQualOperationOutcome' and
        /// '/pre-qualification/write-pre-qual-operation-outcome' — which MUST be set to the same value.
        /// </para>
        /// </summary>
        public const string ValidationServiceKey = "pre-qualification.write-pre-qual-operation-outcome";

        /// <summary>
        /// The Validation service's row as it is stored in Azure App Configuration, in that store's
        /// '/'-separated convention. Report can read this row directly — its selector loads every
        /// null-label key and the .NET provider passes key names through verbatim — which is what
        /// makes the startup drift check possible. Spring maps the '/' to '.', so Validation binds the
        /// same row as <see cref="ValidationServiceKey"/>.
        /// </summary>
        public const string ValidationServiceAppConfigurationKey = "/pre-qualification/write-pre-qual-operation-outcome";

        /// <summary>
        /// When true, the Validation service is the sole writer of the pre-qualification
        /// OperationOutcome to the patient NDJSON (LEGLINK-425) and Report skips its own
        /// legacy "Patient has failed Validation" append. When false, Report retains that write.
        /// <para>
        /// Both runtimes must be set together. With only the Java side enabled the patient NDJSON gets
        /// two OperationOutcomes; with only this side enabled it gets none. Neither failure is loud.
        /// </para>
        /// </summary>
        public bool WritePreQualOperationOutcome { get; set; }
    }
}
