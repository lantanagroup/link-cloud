namespace LantanaGroup.Link.DMRP.Config
{
    /// <summary>
    /// Settings that control the DMRP module hosted by the Tenant service.
    /// </summary>
    public class DmrpSettings
    {
        public const string ConfigSectionName = "DMRP";

        /// <summary>
        /// When false, none of the DMRP controllers, persistence or scheduling behavior is registered
        /// and the host continues to perform facility dQM reporting on its own.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// How to reach the DMRP API. Absent until an environment is pointed at one, which is why
        /// nothing here has a default: a base URL guessed wrong is worse than one left unset, since
        /// it turns a startup problem into a runtime call somewhere unexpected.
        /// </summary>
        public DmrpApiSettings Api { get; set; } = new();

        /// <summary>
        /// The nightly job that turns reporting plans into scheduled reports. Only read when
        /// <see cref="Enabled"/> is true.
        /// </summary>
        public DmrpSchedulingSettings Scheduling { get; set; } = new();
    }

    /// <summary>
    /// Credentials and addresses for the DMRP API - the third-party service that says what a
    /// facility is enrolled to report.
    /// </summary>
    /// <remarks>
    /// The API is reached in two steps: a client-credentials token from <see cref="TokenUrl"/>,
    /// then the reporting-plan operations under <see cref="BaseUrl"/> carrying it as a bearer
    /// token. In the lower environments both are served by the mock.
    /// </remarks>
    public class DmrpApiSettings
    {
        /// <summary>
        /// Root the reporting-plan operations hang off. The operations sit at the root of the
        /// service - /msc and /ps/annual/mrp - because it impersonates nobody else's prefix.
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>The token endpoint the client-credentials grant is posted to.</summary>
        public string? TokenUrl { get; set; }

        public string? ClientId { get; set; }

        public string? ClientSecret { get; set; }

        /// <summary>
        /// Optional scope to request. Omitted from the token request when unset, which is what the
        /// mock expects; a real authorization server may require one.
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// How long before a token's stated expiry it stops being reused, in seconds.
        /// </summary>
        /// <remarks>
        /// A token that expires in flight fails the call it was fetched for. Renewing slightly
        /// early costs one extra token request per lifetime and removes that whole class of
        /// failure, so the margin is here rather than in retry handling.
        /// </remarks>
        public int TokenExpiryMarginSeconds { get; set; } = 60;

        /// <summary>
        /// How long a single DMRP call may take before it is abandoned, in seconds.
        /// </summary>
        /// <remarks>
        /// These calls run inside an admin GET rather than a background job, so the default
        /// <see cref="HttpClient"/> timeout of 100 seconds is the wrong order of magnitude: it holds
        /// the request open long past the point the operator has given up, and long past most proxy
        /// and gateway timeouts in front of it. Thirty seconds is generous for a plan lookup and
        /// still answers while someone is watching.
        /// </remarks>
        public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

        internal const int DefaultTimeoutSeconds = 30;
        internal const int MinimumTimeoutSeconds = 1;

        /// <summary>
        /// Ten minutes, which is already far past anything a call inside an admin GET could be
        /// waiting usefully for, and well clear of the roughly 24 days at which
        /// <see cref="HttpClient.Timeout"/> refuses the value outright.
        /// </summary>
        internal const int MaximumTimeoutSeconds = 600;

        /// <summary>
        /// The configured timeout, or the default when the configured value is not one a call could
        /// sensibly use.
        /// </summary>
        /// <remarks>
        /// Falling back rather than throwing, and rather than honouring the value: HttpClient rejects
        /// anything over Int32.MaxValue milliseconds, and because the client is configured when it is
        /// first created rather than at startup, honouring a nonsense value would surface as a failed
        /// refresh rather than a service that would not boot. This matches how an unusable facility
        /// timezone is handled elsewhere in the module -- fall back to something workable rather than
        /// fail the request over a configuration value.
        /// </remarks>
        public TimeSpan ResolvedTimeout =>
            TimeSpan.FromSeconds(TimeoutSeconds is >= MinimumTimeoutSeconds and <= MaximumTimeoutSeconds
                ? TimeoutSeconds
                : DefaultTimeoutSeconds);

        /// <summary>True when enough is configured to attempt a call.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl)
            && !string.IsNullOrWhiteSpace(TokenUrl)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    /// <summary>
    /// When and how hard the nightly scheduling job runs. Every value has a default and an accepted
    /// range; a value outside the range falls back to the default rather than failing the boot,
    /// matching <see cref="DmrpApiSettings.TimeoutSeconds"/>.
    /// </summary>
    public sealed class DmrpSchedulingSettings
    {
        public const string DefaultNightlyCron = "0 59 23 * * ?";
        public const int DefaultConcurrency = 4;
        public const int DefaultCatchUpNights = 3;

        /// <summary>Quartz cron, evaluated in each facility timezone. Default 23:59 every night.</summary>
        public string NightlyCron { get; set; } = DefaultNightlyCron;

        /// <summary>How many facilities one fire works on at once. Range 1-32.</summary>
        public int Concurrency { get; set; } = DefaultConcurrency;

        /// <summary>
        /// On how many nights at the start of a month a facility with no plan rows for that month is
        /// refreshed from DMRP. Bounds fleet-wide probing of facilities that are enrolled in nothing.
        /// Range 0-28; 0 disables catch-up.
        /// </summary>
        public int CatchUpNights { get; set; } = DefaultCatchUpNights;

        public string ResolvedNightlyCron =>
            !string.IsNullOrWhiteSpace(NightlyCron) && Quartz.CronExpression.IsValidExpression(NightlyCron)
                ? NightlyCron
                : DefaultNightlyCron;

        /// <summary>
        /// The local time of day <see cref="ResolvedNightlyCron"/> fires at, or null when the cron
        /// does not name exactly one.
        /// </summary>
        /// <remarks>
        /// Quartz's FireOnceNow misfire handling moves a recovered fire's scheduled time to the
        /// recovery instant, so the fire time alone cannot say which night was missed. Comparing it
        /// against this nominal time can: a fire that lands earlier in the local day than the cron
        /// would ever fire is a recovery of the night before. A cron whose seconds, minutes or hours
        /// field is a wildcard, list, range or step fires at more than one time of day and so has no
        /// nominal time; it returns null and the comparison is skipped.
        /// </remarks>
        public TimeSpan? ResolvedNightlyLocalTime
        {
            get
            {
                var fields = ResolvedNightlyCron.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (fields.Length < 3
                    || !int.TryParse(fields[0], out var seconds) || seconds is < 0 or > 59
                    || !int.TryParse(fields[1], out var minutes) || minutes is < 0 or > 59
                    || !int.TryParse(fields[2], out var hours) || hours is < 0 or > 23)
                {
                    return null;
                }

                return new TimeSpan(hours, minutes, seconds);
            }
        }

        public int ResolvedConcurrency => Concurrency is >= 1 and <= 32 ? Concurrency : DefaultConcurrency;

        public int ResolvedCatchUpNights => CatchUpNights is >= 0 and <= 28 ? CatchUpNights : DefaultCatchUpNights;
    }
}
