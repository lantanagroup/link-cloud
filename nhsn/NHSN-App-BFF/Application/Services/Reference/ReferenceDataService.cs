using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reference;

// BFF-owned reference data: the vendor profiles and the time zone list. Encounter codes and
// HSLOC codes are read live from Terminology and Normalization respectively — see
// GetEncounterCodesAsync/LookupEncounterCodeAsync and GetHslocCodesAsync.
public sealed class ReferenceDataService : IReferenceDataService
{
    private const string NormalizationServiceName = "Normalization";
    private const string TerminologyServiceName = "Terminology";

    private static readonly JsonSerializerOptions FhirJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly INormalizationServiceClient _normalizationClient;
    private readonly ITerminologyServiceClient _terminologyClient;
    private readonly EncounterCodeSettings _encounterCodeSettings;
    private readonly ILogger<ReferenceDataService> _logger;

    public ReferenceDataService(
        INormalizationServiceClient normalizationClient,
        ITerminologyServiceClient terminologyClient,
        IOptions<EncounterCodeSettings> encounterCodeSettings,
        ILogger<ReferenceDataService> logger)
    {
        _normalizationClient = normalizationClient;
        _terminologyClient = terminologyClient;
        _encounterCodeSettings = encounterCodeSettings.Value;
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

    public async Task<IReadOnlyList<EncounterCode>> GetEncounterCodesAsync(CancellationToken cancellationToken = default)
    {
        var codes = new List<EncounterCode>();

        foreach (var (system, url) in _encounterCodeSettings.ValueSetUrls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var response = await _terminologyClient.ExpandValueSetAsync(url: url, cancellationToken: cancellationToken);
            var body = LinkResponseHandler.Optional<string>(response, TerminologyServiceName, nameof(GetEncounterCodesAsync));
            if (body is null)
            {
                _logger.LogInformation(
                    "No ValueSet loaded in Terminology for encounter code system {System} (url {Url}); skipping.", system, url);
                continue;
            }

            var valueSet = DeserializeFhir<ValueSetJson>(body, TerminologyServiceName, nameof(GetEncounterCodesAsync));
            foreach (var contains in valueSet?.Expansion?.Contains ?? [])
            {
                if (string.IsNullOrWhiteSpace(contains.Code) || string.IsNullOrWhiteSpace(contains.Display))
                {
                    continue;
                }

                var resolvedSystem = string.IsNullOrWhiteSpace(contains.System) ? system : contains.System;

                codes.Add(new EncounterCode
                {
                    System = resolvedSystem,
                    Code = contains.Code,
                    Display = contains.Display,
                    Category = null,
                    CategoryName = null
                });
            }
        }

        return codes;
    }

    public async Task<EncounterCodeDetail?> LookupEncounterCodeAsync(string system, string code, CancellationToken cancellationToken = default)
    {
        var response = await _terminologyClient.LookupCodeInCodeSystemAsync(system: system, code: code, cancellationToken: cancellationToken);
        var body = LinkResponseHandler.Optional<string>(response, TerminologyServiceName, nameof(LookupEncounterCodeAsync));
        if (body is null)
        {
            return null;
        }

        var parameters = DeserializeFhir<ParametersJson>(body, TerminologyServiceName, nameof(LookupEncounterCodeAsync));
        var display = parameters?.Parameter?.FirstOrDefault(p => p.Name == "display")?.ValueString;
        if (string.IsNullOrWhiteSpace(display))
        {
            return null;
        }

        return new EncounterCodeDetail
        {
            System = system,
            Code = code,
            Display = display,
            Name = parameters?.Parameter?.FirstOrDefault(p => p.Name == "name")?.ValueString,
            Version = parameters?.Parameter?.FirstOrDefault(p => p.Name == "version")?.ValueString
        };
    }

    private static T? DeserializeFhir<T>(string json, string service, string operation) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, FhirJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new LinkServiceException(service, operation, 0, null, json, null, ex);
        }
    }

    // The NHSN HSLOC reference vocabulary, read live from Normalization's HSLOC reference-data,
    // Normalization's HSLOC entity has no Category/Type/Definition/
    // FacilityTypes fields, so those stay null on every row; Code and Display are the only fields
    // HslocCode requires.
    public async Task<IReadOnlyList<HslocCode>> GetHslocCodesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _normalizationClient.GetHslocCodesAsync(includeInactive: false, cancellationToken: cancellationToken);
        var rows = LinkResponseHandler.OptionalFromRawBody<List<HslocReferenceCodeJson>>(response, NormalizationServiceName, nameof(GetHslocCodesAsync))
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
