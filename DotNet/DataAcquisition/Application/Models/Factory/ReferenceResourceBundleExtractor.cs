using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Factory;

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
            if (validResourceTypes.Contains(property.ElementName, StringComparer.InvariantCultureIgnoreCase))
            {
                results.Add(property.Value);
            }

            Walk(property.Value, results, validResourceTypes);
        }
    }
}
