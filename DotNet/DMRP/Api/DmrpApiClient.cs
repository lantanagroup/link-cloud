using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LantanaGroup.Link.DMRP.Api
{
    /// <summary>
    /// One measure a facility is enrolled to report for a period, as the DMRP API returned it and
    /// tagged with the component it came back from.
    /// </summary>
    /// <param name="Component">MSC or PS, decided by which operation returned the entry.</param>
    /// <param name="Measure">The NHSN measure, for example HOB.</param>
    /// <param name="ReportingMonth">Month of the reporting period, 1-12.</param>
    /// <param name="ReportingYear">Year of the reporting period.</param>
    public sealed record DmrpReportingPlanEntry(string Component, string Measure, int ReportingMonth, int ReportingYear);

    /// <summary>
    /// Reads a facility's reporting plan from the DMRP API.
    /// </summary>
    public interface IDmrpApiClient
    {
        /// <summary>
        /// Everything DMRP says the facility is enrolled to report for a period, across both
        /// components.
        /// </summary>
        /// <remarks>
        /// A facility enrolled in nothing comes back empty, which is a meaningful answer: DMRP has
        /// no negative representation, so "enrolled in nothing" and "no rows" are the same thing.
        /// </remarks>
        /// <exception cref="DmrpApiException">Either operation could not be read.</exception>
        Task<IReadOnlyList<DmrpReportingPlanEntry>> GetReportingPlanAsync(string facilityId, int month, int year,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Calls the two reporting-plan operations and combines them.
    /// </summary>
    /// <remarks>
    /// The API splits a facility's plan across two operations by component, and Link wants the whole
    /// plan, so both are read and the results concatenated with the component each came from
    /// attached. The component is not in the payload - it is knowable only from which operation was
    /// called - which is why it is stamped on here rather than read off an entry.
    /// <para>
    /// The two calls are sequential rather than concurrent. They share one token and one connection,
    /// the payloads are small, and a facility's sync is not on anyone's critical path; running them
    /// in parallel would buy nothing and make a partial failure harder to reason about.
    /// </para>
    /// </remarks>
    public sealed class DmrpApiClient : IDmrpApiClient
    {
        /// <summary>Named so the token provider and the plan reads share one handler.</summary>
        public const string HttpClientName = "DmrpApi";

        internal const string MedicinePath = "msc";
        internal const string PatientSafetyPath = "ps/annual/mrp";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDmrpApiTokenProvider _tokens;
        private readonly IOptions<DmrpSettings> _settings;
        private readonly ILogger<DmrpApiClient> _logger;

        public DmrpApiClient(IHttpClientFactory httpClientFactory, IDmrpApiTokenProvider tokens,
            IOptions<DmrpSettings> settings, ILogger<DmrpApiClient> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<DmrpReportingPlanEntry>> GetReportingPlanAsync(string facilityId, int month,
            int year, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);

            var api = _settings.Value.Api;

            if (!api.IsConfigured)
            {
                throw new DmrpApiException(
                    "The DMRP API is not configured. DMRP:Api needs BaseUrl, TokenUrl, ClientId and ClientSecret.");
            }

            var token = await _tokens.GetAsync(cancellationToken);

            var entries = new List<DmrpReportingPlanEntry>();

            entries.AddRange(await ReadAsync(api, token, ReportingComponents.Msc, MedicinePath,
                facilityId, month, year, cancellationToken));

            entries.AddRange(await ReadAsync(api, token, ReportingComponents.Ps, PatientSafetyPath,
                facilityId, month, year, cancellationToken));

            _logger.LogInformation(
                "DMRP returned {Count} enrollment(s) for facility {FacilityId} for {Month}/{Year}",
                entries.Count, facilityId.SanitizeForLog(), month, year);

            return entries;
        }

        private async Task<IReadOnlyList<DmrpReportingPlanEntry>> ReadAsync(DmrpApiSettings api, string token,
            string component, string path, string facilityId, int month, int year,
            CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);

            client.BaseAddress = new Uri(api.BaseUrl!.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{path}?nhsnorgid={Uri.EscapeDataString(facilityId)}" +
                      $"&year={year.ToString(CultureInfo.InvariantCulture)}" +
                      $"&month={month.ToString(CultureInfo.InvariantCulture)}";

            HttpResponseMessage response;

            try
            {
                response = await client.GetAsync(url, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new DmrpApiException($"The DMRP API operation /{path} could not be reached.", ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new DmrpApiException(
                        $"The DMRP API operation /{path} answered {(int)response.StatusCode}.");
                }

                var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>(cancellationToken);

                if (plan is null)
                {
                    throw new DmrpApiException($"The DMRP API operation /{path} returned no reporting plan.");
                }

                return Project(plan, component, month, year, path);
            }
        }

        /// <summary>
        /// Turns the payload into entries, dropping anything Link cannot record against a period.
        /// </summary>
        /// <remarks>
        /// An entry carries its own month and year, and they are used in preference to the ones
        /// asked for - a response that answers for a different period than the request is reporting
        /// something, and quietly relabelling it to the requested period would hide it. When an
        /// entry omits them, the requested period is the only thing they can be.
        /// </remarks>
        private IReadOnlyList<DmrpReportingPlanEntry> Project(ReportingPlanResponse plan, string component,
            int requestedMonth, int requestedYear, string path)
        {
            var entries = new List<DmrpReportingPlanEntry>(plan.Plans.Count);

            foreach (var item in plan.Plans)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    // Nothing identifies what the facility is enrolled in, so there is nothing to
                    // record and nothing an admin could map later.
                    _logger.LogWarning(
                        "The DMRP API operation /{Path} returned an entry with no measure name; it was skipped.",
                        path.SanitizeForLog());

                    continue;
                }

                entries.Add(new DmrpReportingPlanEntry(
                    component,
                    item.Name.Trim(),
                    item.Month ?? requestedMonth,
                    item.Year ?? requestedYear));
            }

            return entries;
        }
    }

    /// <summary>Raised when the DMRP API could not be read.</summary>
    public sealed class DmrpApiException : Exception
    {
        public DmrpApiException(string message) : base(message)
        {
        }

        public DmrpApiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
