using LantanaGroup.Link.DataAcquisition.Application.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.DataAcquisition.Application.Builders;

public class BundleBuilder
{
    private readonly List<(string fullUrl, object resource)> _resources;
    public BundleBuilder(List<(string fullUrl, object resource)> resources)
    {
        _resources = resources;
    }

    public Bundle Build()
    {
        var bundle = new Bundle();
        foreach (var resource in _resources)
        {
            var entry = new Entry
            {
                FullUrl = resource.fullUrl,
                Resource = resource.resource
            };
            bundle.Entry.Add(entry);
        }

        return bundle;
    }
}
