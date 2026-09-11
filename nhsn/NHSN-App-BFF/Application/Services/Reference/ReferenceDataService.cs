using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.EncounterCodes;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reference;

// BFF-owned reference data: the vendor profiles, the time zone list, and the encounter code
public sealed class ReferenceDataService : IReferenceDataService
{
    private const string ServiceName = "Normalization";

    private readonly INormalizationServiceClient _normalizationClient;
    private readonly ILogger<ReferenceDataService> _logger;

    public ReferenceDataService(INormalizationServiceClient normalizationClient, ILogger<ReferenceDataService> logger)
    {
        _normalizationClient = normalizationClient;
        _logger = logger;
    }

    // Curated US time zone ids, in display order.
    private static readonly string[] OrderedTimezoneIds =
    {
        "America/New_York",
        "America/Detroit",
        "America/Kentucky/Louisville",
        "America/Kentucky/Monticello",
        "America/Indiana/Indianapolis",
        "America/Indiana/Vincennes",
        "America/Indiana/Winamac",
        "America/Indiana/Marengo",
        "America/Indiana/Petersburg",
        "America/Indiana/Vevay",
        "America/Indiana/Tell_City",
        "America/Indiana/Knox",
        "America/Chicago",
        "America/Menominee",
        "America/North_Dakota/Center",
        "America/North_Dakota/New_Salem",
        "America/North_Dakota/Beulah",
        "America/Denver",
        "America/Boise",
        "America/Phoenix",
        "America/Los_Angeles",
        "America/Anchorage",
        "America/Juneau",
        "America/Sitka",
        "America/Metlakatla",
        "America/Yakutat",
        "America/Nome",
        "America/Adak",
        "Pacific/Honolulu",
        "America/Puerto_Rico",
        "Pacific/Guam",
        "Pacific/Saipan",
        "Pacific/Pago_Pago"
    };

    private static readonly Lazy<IReadOnlyList<TimezoneResponse>> Timezones = new(BuildTimezones);

    public IReadOnlyList<VendorProfile> GetVendorProfiles() => VendorProfileCatalog.All;

    public IReadOnlyList<TimezoneResponse> GetTimezones() => Timezones.Value;

    public IReadOnlyList<EncounterCode> GetEncounterCodes(string? query = null)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return EncounterCodeCatalog.All;
        }

        return EncounterCodeCatalog.All
            .Where(code => $"{code.System} {code.Code} {code.Display} {code.Category} {code.CategoryName}"
                .Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    // The NHSN HSLOC reference vocabulary, read live from Normalization's HSLOC reference-data,
    // Normalization's HSLOC entity has no Category/Type/Definition/
    // FacilityTypes fields, so those stay null on every row; Code and Display are the only fields
    // HslocCode requires.
    public async Task<IReadOnlyList<HslocCode>> GetHslocCodesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _normalizationClient.GetHslocCodesAsync(includeInactive: false, cancellationToken: cancellationToken);
        var rows = LinkResponseHandler.OptionalFromRawBody<List<HslocReferenceCodeJson>>(response, ServiceName, nameof(GetHslocCodesAsync))
                   ?? [];

        var codes = new List<HslocCode>(rows.Count);
        foreach (var row in rows)
        {
            var display = string.IsNullOrWhiteSpace(row.ShortDescription) ? row.LongDescription : row.ShortDescription;

            if (string.IsNullOrWhiteSpace(row.HSLOCCode) || string.IsNullOrWhiteSpace(display))
            {
                _logger.LogWarning(
                    "Skipping HSLOC reference code {Id}: missing required field(s) (HSLOCCode={HSLOCCode}, ShortDescription={ShortDescription}, LongDescription={LongDescription}).",
                    row.Id, row.HSLOCCode, row.ShortDescription, row.LongDescription);
                continue;
            }

            codes.Add(new HslocCode
            {
                Code = row.HSLOCCode,
                Display = display
            });
        }

        return codes;
    }

    private static IReadOnlyList<TimezoneResponse> BuildTimezones() =>
        OrderedTimezoneIds
            .Select(id => new TimezoneResponse
            {
                Id = id,
                DisplayName = $"{id} — {TimeZoneInfo.FindSystemTimeZoneById(id).DisplayName}"
            })
            .ToArray();
}
