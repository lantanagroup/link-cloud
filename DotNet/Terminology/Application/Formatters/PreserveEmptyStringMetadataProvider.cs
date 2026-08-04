using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace LantanaGroup.Link.Terminology.Application.Formatters;

/// <summary>
/// Keeps an empty string supplied by a client as an empty string during model binding, rather than
/// converting it to null.
/// </summary>
/// <remarks>
/// MVC's default is <c>ConvertEmptyStringToNull = true</c>, which makes <c>?system=</c> arrive at an action
/// as null and therefore indistinguishable from an omitted parameter. Those are not the same request: an
/// omitted system means "search every code system in the value set", while a blank one is malformed FHIR
/// and has to be rejected with a 400 (LEGLINK-888). Validation cannot make a distinction the binder has
/// already erased, so the transformation is turned off rather than worked around in the controller.
///
/// Every other query parameter in this service guards with <c>string.IsNullOrEmpty</c> or
/// <c>string.IsNullOrWhiteSpace</c> — including ConfigController's <c>version</c>, which normalizes blank
/// to null itself — so preserving the empty string leaves their behavior unchanged.
/// </remarks>
public class PreserveEmptyStringMetadataProvider : IDisplayMetadataProvider
{
    /// <summary>
    /// Clears <c>ConvertEmptyStringToNull</c> for every bound model in the service.
    /// </summary>
    /// <param name="context">The metadata being built for a model.</param>
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        context.DisplayMetadata.ConvertEmptyStringToNull = false;
    }
}
