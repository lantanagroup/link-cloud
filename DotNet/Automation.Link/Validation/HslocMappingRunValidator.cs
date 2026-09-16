using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Automation.Link.Validation;

public sealed class HslocMappingRunValidator
{
    private const int MaxErrors = 100;
    private readonly IAutomationOutput _output;
    private readonly INormalizationServiceClient _client;

    public HslocMappingRunValidator(IAutomationOutput output, INormalizationServiceClient client)
    {
        _output = output;
        _client = client;
    }

    public async Task ValidateAllAsync(
        string facilityId,
        bool hslocMapEnabled,
        IReadOnlyList<string>? generatedPatientIds = null,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        try
        {
            var mappings = await SearchAllMappingsAsync(facilityId, unmapped: null, cancellationToken);

            if (!hslocMapEnabled)
            {
                if (mappings.Count > 0)
                {
                    AddError(errors,
                        $"Facility '{facilityId}' has no HSLOCMap operation but hsloc-mappings search returned {mappings.Count} row(s).");
                }
            }
            else
            {
                var codes = await GetHslocCodesAsync(cancellationToken);
                if (codes.Count == 0)
                {
                    AddError(errors, "GET /api/normalization/HSLOC returned no active codes. Mapping HSLOCId cannot be resolved.");
                }
                else
                {
                    ValidateMappedAndUnmapped(facilityId, mappings, generatedPatientIds, errors);
                    await ValidateFacilityLocationsAsync(facilityId, generatedPatientIds, errors, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AddError(errors, $"Unhandled exception during HSLOC mapping run validation: {ex.Message}");
        }

        if (errors.Count == 0)
        {
            _output.WriteLine("HSLOC MAPPING RUN VALIDATION: Passed");
            return;
        }

        _output.WriteLine($"HSLOC MAPPING RUN VALIDATION: Failed ({errors.Count} issue(s))");
        foreach (var error in errors)
            _output.WriteLine($"  - {error}");

        throw new InvalidOperationException($"HSLOC MAPPING RUN VALIDATION failed with {errors.Count} issue(s). {errors[0]}");
    }

    private async Task<List<HslocCodeApiModel>> GetHslocCodesAsync(CancellationToken cancellationToken)
    {
        var resp = await _client.GetHslocCodesAsync(cancellationToken: cancellationToken);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"GET /api/normalization/HSLOC failed with HTTP {resp.StatusCode}.");
        return (resp.Body ?? [])
            .Where(code => code.IsActive && !string.IsNullOrWhiteSpace(code.HSLOCCode))
            .ToList();
    }

    private async Task<List<FacilityLocationLocalCodeMappingApiModel>> SearchAllMappingsAsync(
        string facilityId,
        bool? unmapped,
        CancellationToken cancellationToken)
    {
        var all = new List<FacilityLocationLocalCodeMappingApiModel>();
        var page = 1;
        while (true)
        {
            var resp = await _client.SearchFacilityLocationLocalCodeMappingsAsync(
                new SearchFacilityLocationLocalCodeMappingsRequestApiModel
                {
                    FacilityId = facilityId,
                    Unmapped = unmapped,
                    PageSize = 100,
                    PageNumber = page
                },
                cancellationToken);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"GET /api/normalization/hsloc-mappings/search failed with HTTP {resp.StatusCode}.");

            var records = resp.Body?.Records ?? [];
            all.AddRange(records);

            var totalPages = resp.Body?.Metadata?.TotalPages ?? 1;
            if (page >= totalPages || records.Count == 0)
                break;
            page++;
        }

        return all;
    }

    private static void ValidateMappedAndUnmapped(
        string facilityId,
        IReadOnlyList<FacilityLocationLocalCodeMappingApiModel> mappings,
        IReadOnlyList<string>? generatedPatientIds,
        List<string> errors)
    {
        if (mappings.Count == 0)
        {
            AddError(errors, $"Expected HSLOC mapping rows for facility '{facilityId}' after an HSLOCMap run, but search returned none.");
            return;
        }

        var identifierMapped = mappings.Where(m =>
            string.Equals(m.LocalCodeSystem, HslocMappingDefaults.IdentifierSystem, StringComparison.OrdinalIgnoreCase)
            && m.HSLOCId.HasValue
            && !string.IsNullOrWhiteSpace(m.HSLOCCode)
            && !MappingTargetSystems.IsHsloc(m.LocalCodeSystem)).ToList();

        var roleMapped = mappings.Where(m =>
            string.Equals(m.LocalCodeSystem, HslocMappingDefaults.RoleCodeSystem, StringComparison.OrdinalIgnoreCase)
            && m.HSLOCId.HasValue
            && !string.IsNullOrWhiteSpace(m.HSLOCCode)).ToList();

        var runTag = FhirGenerationPipeline.TryInferRunTag(generatedPatientIds ?? []);
        if (!string.IsNullOrWhiteSpace(runTag))
        {
            var ids = new FhirBundleGenerator.SharedIds(runTag);
            var icuHsloc = HslocMappingDefaults.RoleCodeToHsloc["ICU"].Code;
            var icuIdentifierMapped = mappings.Any(m =>
                string.Equals(m.LocationId, ids.IcuLocation, StringComparison.Ordinal)
                && string.Equals(m.LocalCodeSystem, HslocMappingDefaults.IdentifierSystem, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.LocalCode, ids.IcuLocation, StringComparison.Ordinal)
                && m.HSLOCId.HasValue
                && string.Equals(m.HSLOCCode, icuHsloc, StringComparison.OrdinalIgnoreCase));
            if (!icuIdentifierMapped)
            {
                AddError(errors,
                    $"No mapped identifier row for ICU Location '{ids.IcuLocation}' " +
                    $"(LocalCodeSystem={HslocMappingDefaults.IdentifierSystem}, LocalCode={ids.IcuLocation}, HSLOCCode={icuHsloc}). " +
                    "A hospital-only or pre-stamped HSLOC type coding is not enough.");
            }
        }
        else if (identifierMapped.Count == 0 && roleMapped.Count == 0)
        {
            AddError(errors,
                "HSLOCMap ran but no mapping row has HSLOCId set for a local (non-HSLOC) code system.");
        }

        var unmapped = mappings.Where(m => !m.HSLOCId.HasValue).ToList();
        if (unmapped.Count == 0)
        {
            AddError(errors,
                "Expected unmapped local codes (HSLOCId null) for leftover type systems such as location-alias. Search returned none.");
        }
    }

    private async Task ValidateFacilityLocationsAsync(
        string facilityId,
        IReadOnlyList<string>? generatedPatientIds,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var runTag = FhirGenerationPipeline.TryInferRunTag(generatedPatientIds ?? []);
        if (string.IsNullOrWhiteSpace(runTag))
            return;

        var ids = new FhirBundleGenerator.SharedIds(runTag);
        var icu = await _client.GetFacilityLocationAsync(facilityId, ids.IcuLocation, cancellationToken);
        if (!icu.IsSuccessStatusCode || icu.Body == null)
        {
            AddError(errors, $"GET facility-locations for {ids.IcuLocation} failed with HTTP {icu.StatusCode}.");
            return;
        }

        if (string.IsNullOrWhiteSpace(icu.Body.LocationName))
            AddError(errors, $"FacilityLocations row for {ids.IcuLocation} is missing LocationName.");
        if (string.IsNullOrWhiteSpace(icu.Body.LocationAlias))
            AddError(errors, $"FacilityLocations row for {ids.IcuLocation} is missing LocationAlias.");
        if (!string.Equals(icu.Body.PartOfId, ids.HospitalLocation, StringComparison.Ordinal))
        {
            AddError(errors,
                $"Expected ICU location PartOfId '{ids.HospitalLocation}' but was '{icu.Body.PartOfId}'.");
        }
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }
}
