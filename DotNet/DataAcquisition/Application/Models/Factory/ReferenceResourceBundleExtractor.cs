using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Factory;

public class ReferenceResourceBundleExtractor
{
    public static List<ResourceReference> Extract(Resource resource, List<string> validResourceTypes)
    {
        return
        Collect(resource, validResourceTypes)
            .Where(x => x is ResourceReference reference && validResourceTypes.Contains(reference.Reference, StringComparer.InvariantCultureIgnoreCase))
            .Select(x => (ResourceReference)x)
            .ToList();
    }

    public static List<ResourceReference> Extract(Bundle bundle, List<string> validResourceTypes)
    {
        return
        bundle
            .Entry
            .SelectMany(x => Collect(x.Resource, validResourceTypes))
            .Where(x => x is ResourceReference reference && validResourceTypes.Contains(reference.Reference, StringComparer.InvariantCultureIgnoreCase))
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
        results.Add(ancestor);

        foreach (var property in ancestor.NamedChildren)
        {
            var candidate = property.Value;
            if(candidate is ResourceReference reference && validResourceTypes.Contains(reference.Reference, StringComparer.InvariantCultureIgnoreCase))
                results.Add(property.Value);

            Walk(property.Value, results, validResourceTypes);
        }
    }
}
