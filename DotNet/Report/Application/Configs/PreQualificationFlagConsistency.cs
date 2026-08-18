using Microsoft.Extensions.Configuration;

namespace LantanaGroup.Link.Report.Application.Options
{
    /// <summary>
    /// Detects disagreement between the two halves of the pre-qualification OperationOutcome flag
    /// (LEGLINK-466). Report and the Java Validation service read separate keys for one decision, so
    /// the pair can be half-configured — and the result is silently wrong submitted data rather than
    /// an error: with only Validation enabled the patient NDJSON carries two OperationOutcomes, with
    /// only Report enabled it carries none.
    /// <para>
    /// Report can see Validation's value because its Azure App Configuration selector loads every
    /// null-label key and the .NET provider passes key names through verbatim, so Validation's
    /// '/'-separated row is readable here as a flat key.
    /// </para>
    /// </summary>
    public static class PreQualificationFlagConsistency
    {
        /// <summary>
        /// Returns true when Validation's value is present and disagrees with Report's effective value.
        /// </summary>
        /// <remarks>
        /// The check is skipped entirely when Validation's row is absent, which is the normal state
        /// anywhere Azure App Configuration is not the configuration source — the local docker-compose
        /// stack, for instance, where each container receives only its own environment variable.
        /// Absence is not disagreement, and warning about it on every local startup would train people
        /// to ignore the message.
        /// <para>
        /// Report's value is compared as its <em>effective</em> value rather than only when explicitly
        /// present, because an absent Report row still means false — and "Report row missing,
        /// Validation row true" is precisely the duplicate-OperationOutcome case worth catching.
        /// </para>
        /// </remarks>
        public static bool TryDetectMismatch(
            IConfiguration configuration,
            bool reportValue,
            out bool validationValue)
        {
            validationValue = false;

            var configured = configuration.GetValue<bool?>(
                PreQualificationSettings.ValidationServiceAppConfigurationKey);

            if (configured is null)
            {
                return false;
            }

            validationValue = configured.Value;

            return validationValue != reportValue;
        }
    }
}
