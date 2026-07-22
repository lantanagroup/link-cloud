namespace LantanaGroup.Link.Report.Application.Options
{
    public class PreQualificationSettings
    {
        public const string Key = "PreQualification";

        /// <summary>
        /// When true, the Validation service is the sole writer of the pre-qualification
        /// OperationOutcome to the patient NDJSON (LEGLINK-425) and Report skips its own
        /// legacy "Patient has failed Validation" append. When false, Report retains that write.
        /// This is the .NET side of a cross-runtime flag; the Java Validation service reads the
        /// equivalent 'pre-qualification.write-pre-qual-operation-outcome' key.
        /// </summary>
        public bool WritePreQualOperationOutcome { get; set; }
    }
}
