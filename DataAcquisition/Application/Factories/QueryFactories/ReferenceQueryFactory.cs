using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories.QueryFactories;

public class ReferenceQueryFactory
{
    public static ReferenceQueryFactoryResult Build(ReferenceQueryConfig config, Hl7.Fhir.Model.Bundle bundle)
    {
        //var test = bundle
        //    .Entry
        //    .SelectMany(x => Collect(x.Resource, config.ResourceType));
        var resources =
            bundle
            .Entry
            .SelectMany(x => Collect(x.Resource, config.ResourceType))
            .Where(x =>
            {
                return x is ResourceReference;
            })
            .Select(x => (ResourceReference)x)
            .ToList();

        return new ReferenceQueryFactoryResult(config.ResourceType, resources);
    }

    private static List<Base> Collect(Base ancestor, string resourceName)
    {
        List<Base> result = new List<Base>();
        Walk(ancestor, resourceName, result);
        return result;
    }

    private static void Walk(Base ancestor, string resourceName, List<Base> results) 
    {
        if (ancestor.TypeName.ToLower() == resourceName.ToLower())
        {
            results.Add(ancestor);
        }

        foreach (var property in ancestor.NamedChildren)
        {
            if (property.ElementName.ToLower() == resourceName.ToLower())
            {
                results.Add(property.Value);
            }

            Walk(property.Value, resourceName, results);
        }
    }
}
