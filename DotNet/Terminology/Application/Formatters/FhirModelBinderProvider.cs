namespace Terminology.Application.Formatters;

using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using System;

public class FhirModelBinderProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (typeof(Resource).IsAssignableFrom(context.Metadata.ModelType))
        {
            return new BinderTypeModelBinder(typeof(FhirModelBinder));
        }

        return null;
    }
}