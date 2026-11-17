using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;

namespace Link.UI.Services;

/// <summary>
/// Tags a run with one configured EHR system and, when the run is still on the
/// generic system facility template, applies that system's default template.
/// A template the run already named, other than the system default, is kept.
/// </summary>
public static class EhrSystemRunSelector
{
    public static async Task<ResolvedRunOptions> ApplyAsync(
        ResolvedRunOptions options,
        StartScenarioRequest request,
        EhrSystemCatalog catalog,
        IFacilityTemplateStore facilityTemplates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(facilityTemplates);

        var ehr = catalog.Resolve(RequestedName(request));
        var templateId = options.FacilityTemplateId;
        if (options.FacilityConfigurationMode == FacilityConfigurationMode.Facility
            && !string.IsNullOrWhiteSpace(ehr.DefaultFacilityTemplate)
            && !KeepsSpecificTemplate(request))
        {
            templateId = await ResolveTemplateIdAsync(ehr, facilityTemplates, cancellationToken);
        }

        return options with
        {
            EhrSystemName = ehr.Name,
            FacilityTemplateId = templateId
        };
    }

    private static string? RequestedName(StartScenarioRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.EhrSystem))
            return request.EhrSystem.Trim();

        return ReadString(request.RunConfigurationJson, "ehrSystem")
            ?? ReadString(request.RunConfigurationJson, "EhrSystem");
    }

    private static bool KeepsSpecificTemplate(StartScenarioRequest request)
    {
        var requested = request.FacilityTemplateId ?? ReadGuid(request.RunConfigurationJson, "facilityTemplateId");
        return requested.HasValue && requested.Value != FacilityTemplateCatalog.SystemDefaultId;
    }

    private static async Task<Guid> ResolveTemplateIdAsync(
        NamedEhrSystem ehr,
        IFacilityTemplateStore facilityTemplates,
        CancellationToken cancellationToken)
    {
        var template = ehr.DefaultFacilityTemplate ?? string.Empty;
        if (Guid.TryParse(template, out var id))
            return id;

        var matches = (await facilityTemplates.GetAllAsync(cancellationToken))
            .Where(candidate => string.Equals(candidate.Name, template, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1)
            return matches[0].Id;
        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Facility template '{template}' on EHR system '{ehr.Name}' was not found.");
        }

        throw new InvalidOperationException(
            $"Facility template name '{template}' on EHR system '{ehr.Name}' matches more than one template.");
    }

    private static string? ReadString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = property.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid? ReadGuid(string? json, string propertyName)
    {
        var text = ReadString(json, propertyName);
        return Guid.TryParse(text, out var id) ? id : null;
    }
}
