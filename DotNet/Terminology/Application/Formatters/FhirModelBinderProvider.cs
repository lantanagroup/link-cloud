using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace LantanaGroup.Link.Terminology.Application.Formatters;

/// <summary>
/// Provides a custom model binder for FHIR resource models in ASP.NET Core applications.
/// </summary>
/// <remarks>
/// The <see cref="FhirModelBinderProvider"/> class implements <see cref="IModelBinderProvider"/>
/// and is designed to supply a model binder for instances of the FHIR `Resource` type or its subclasses.
/// This functionality is useful when working with FHIR-specific models in web APIs that require custom
/// binding logic during model binding in request processing.
/// </remarks>
/// <example>
/// This provider works by checking if the model type being bound derives from
/// <see cref="Hl7.Fhir.Model.Resource"/>. If so, it provides a binder of type <see cref="FhirModelBinder"/>.
/// </example>
public class FhirModelBinderProvider : IModelBinderProvider
{
    /// <summary>
    /// Provides a model binder for FHIR resource models during the model binding process in ASP.NET Core.
    /// </summary>
    /// <param name="context">The <see cref="ModelBinderProviderContext"/> containing information
    /// about the model and its binding context.</param>
    /// <returns>
    /// An instance of the <see cref="IModelBinder"/> for FHIR resource models if the model type
    /// derives from <see cref="Hl7.Fhir.Model.Resource"/>; otherwise, returns null.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="context"/> is null.</exception>
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
            ArgumentNullException.ThrowIfNull(nameof(context));

        if (typeof(Resource).IsAssignableFrom(context.Metadata.ModelType))
        {
            return new BinderTypeModelBinder(typeof(FhirModelBinder));
        }

        return null;
    }
}