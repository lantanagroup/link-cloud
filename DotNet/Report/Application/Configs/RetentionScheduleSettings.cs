using Quartz;

namespace LantanaGroup.Link.Report.Application.Options
{
    /// <summary>
    /// Represents the settings for the retention schedule, including the cron expression and whether
    /// the schedule is enabled.
    /// </summary>
    public class RetentionScheduleSettings
    {
        /// <summary>
        /// Gets the configuration key for the retention schedule settings.
        /// This key is used to bind the settings from the configuration.
        /// </summary>
        public const string Key = "Retention:Schedule";

        /// <summary>
        /// Gets the default cron expression for the retention schedule.
        /// This expression represents a daily schedule at 2 AM UTC.
        /// </summary>
        public const string DefaultCron = "0 0 2 * * ?";

        /// <summary>
        /// Gets or sets the cron expression for the retention schedule. If not set, the default
        /// cron expression will be used.
        /// </summary>
        public string Cron { get; set; } = DefaultCron;

        /// <summary>
        /// Gets or sets a value indicating whether the retention schedule is enabled. If set to false,
        /// the schedule will not be executed.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets a value indicating whether the cron expression is valid. Returns false when the cron expression
        /// is null, empty, whitespace, or not a valid Quartz cron expression.
        /// </summary>
        public bool CronIsValid =>
            !string.IsNullOrWhiteSpace(Cron) && CronExpression.IsValidExpression(Cron);

        /// <summary>
        /// Gets the resolved cron expression for the retention schedule. If the cron expression is invalid,
        /// the default cron expression will be returned.
        /// </summary>
        public string ResolvedCron =>
            CronIsValid ? Cron : DefaultCron;
    }
}
