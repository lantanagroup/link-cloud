using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Factory;

public class ReferenceResourceBundleExtractor
{
    public static List<ResourceReference> Extract(Resource resource)
    {
        return
        Collect(resource)
            .Where(x =>
            {
                return x is ResourceReference;
            })
            .Select(x => (ResourceReference)x)
            .ToList();
    }

    public static List<ResourceReference> Extract(Bundle bundle)
    {
        return
        bundle
            .Entry
            .SelectMany(x => Collect(x.Resource))
            .Where(x =>
            {
                return x is ResourceReference;
            })
            .Select(x => (ResourceReference)x)
            .ToList();
    }

    private static List<Base> Collect(Base ancestor)
    {
        List<Base> result = new List<Base>();
        Walk(ancestor, result);
        return result;
    }

    private static void Walk(Base ancestor, List<Base> results)
    {
        results.Add(ancestor);

        foreach (var property in ancestor.NamedChildren)
        {
            results.Add(property.Value);

            Walk(property.Value, results);
        }
    }
}
