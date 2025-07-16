using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;

public class ReferenceResourceBundleExtractor
{
    public static List<ResourceReference> Extract(Resource resource, List<string> validResourceTypes)
    {
        return
        Collect(resource, validResourceTypes)
            .Where(x =>
            {
                return x is ResourceReference;
            })
            .Select(x => (ResourceReference)x)
            .ToList();
    }

    public static List<ResourceReference> Extract(Bundle bundle, List<string> validResourceTypes)
    {
        return
        bundle
            .Entry
            .SelectMany(x => Collect(x.Resource, validResourceTypes))
            .Where(x =>
            {
                return x is ResourceReference;
            })
            .Select(x => (ResourceReference)x)
            .ToList();
    }

    private static List<Base> Collect(Base ancestor, List<string> validResourceTypes)
    {
        List<Base> result = new List<Base>();
        Walk(ancestor, result, validResourceTypes);
        return result;
    }

    private static void Walk(Base ancestor, List<Base> results, List<string> validResourceTypes)
    {
        if (validResourceTypes.Contains(ancestor.TypeName,StringComparer.InvariantCultureIgnoreCase))
        {
            results.Add(ancestor);
        }

        foreach (var property in ancestor.NamedChildren)
        {
            if (property.Value is ResourceReference reference)
            {
                try
                {
                    ResourceIdentity identity = new(reference.Reference);
                    if (validResourceTypes.Contains(identity.ResourceType))
                    {
                        results.Add(reference);
                    }
                }
                catch (Exception) { }
            }

            Walk(property.Value, results, validResourceTypes);
        }
    }
}
