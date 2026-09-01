using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DMRP.Api
{
    /// <summary>
    /// The DMRP API's reporting-plan response.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than generated from the contract. The contract lives with the mock that
    /// serves it, and generating from there would tie this module's build to a service it must not
    /// depend on. Only the fields Link reads are declared; the rest of the payload is ignored,
    /// which is also what keeps a new field upstream from breaking a deserialize here.
    /// </remarks>
    public sealed class ReportingPlanResponse
    {
        [JsonPropertyName("plans")]
        public List<ReportingPlanItem> Plans { get; set; } = [];
    }

    /// <summary>
    /// One measure a facility is enrolled to report, as DMRP returns it.
    /// </summary>
    /// <remarks>
    /// Only measures the facility IS enrolled in appear. Absence is how DMRP says "not enrolled" -
    /// there is no negative representation - which is why a sync has to compare against what it
    /// previously recorded rather than trust the response to mention a withdrawal.
    /// </remarks>
    public sealed class ReportingPlanItem
    {
        /// <summary>The NHSN measure, for example HOB. Link maps this to a dQM itself.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The facility, as a string here. It is a number at the root of the response - an
        /// asymmetry in the real API that is reproduced rather than tidied up.
        /// </summary>
        [JsonPropertyName("nhsnorgid")]
        public string? NhsnOrgId { get; set; }

        [JsonPropertyName("month")]
        public int? Month { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Only ever "Y". Enrollment is conveyed by the entry being present at all, so this carries
        /// nothing a caller can act on.
        /// </summary>
        [JsonPropertyName("reporting")]
        public string? Reporting { get; set; }
    }

    /// <summary>The client-credentials grant, as the DMRP token endpoint expects it.</summary>
    /// <remarks>
    /// The property names are the OAuth 2.0 ones - <c>grant_type</c>, <c>client_id</c> - carried in
    /// a JSON body rather than the form encoding the specification describes, because that is what
    /// the endpoint accepts.
    /// </remarks>
    public sealed class DmrpTokenRequest
    {
        [JsonPropertyName("grant_type")]
        public string GrantType { get; set; } = string.Empty;

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Omitted from the body entirely when unset, rather than sent as null.</summary>
        [JsonPropertyName("scope")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scope { get; set; }
    }

    /// <summary>The token endpoint's response to a client-credentials grant.</summary>
    public sealed class DmrpTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}
