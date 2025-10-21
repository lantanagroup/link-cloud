using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace LantanaGroup.Link.Terminology.Application.Formatters;

/// <summary>
/// Keeps an empty string supplied by a client as an empty string during model binding, rather than
/// converting it to null, for parameters marked with <see cref="PreserveEmptyStringAttribute"/>.
/// </summary>
/// <remarks>
/// MVC's default is <c>ConvertEmptyStringToNull = true</c>, which makes <c>?system=</c> arrive at an action
/// as null and therefore indistinguishable from an omitted parameter. Those are not the same request: an
/// omitted system means "search every code system in the value set", while a blank one is malformed FHIR
/// and has to be rejected with a 400 (LEGLINK-888). Validation cannot make a distinction the binder has
/// already erased, so the transformation is turned off rather than worked around in the controller.
///
/// The attribute keeps that off-switch to the one parameter that needs it, leaving every other bound value
/// in the service on MVC's default behavior.
/// </remarks>
public class PreserveEmptyStringMetadataProvider : IDisplayMetadataProvider
{
    /// <summary>
    /// Clears <c>ConvertEmptyStringToNull</c> for a parameter carrying <see cref="PreserveEmptyStringAttribute"/>.
    /// </summary>
    /// <param name="context">The metadata being built for a model.</param>
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        ParameterInfo? parameter = context.Key.ParameterInfo;
        if (parameter is not null && parameter.IsDefined(typeof(PreserveEmptyStringAttribute), inherit: false))
        {
            context.DisplayMetadata.ConvertEmptyStringToNull = false;
        }
    }
}
