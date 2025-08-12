using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;

namespace LantanaGroup.Link.Report.Services.ResourceMerger;

public class ResourceMerger
{
    private IResourceMergeStrategy? _strategy;
    
    public void SetStrategy(IResourceMergeStrategy strategy)
    {
        _strategy = strategy;
    }
    
    public Resource Merge(
        Resource oldResource,
        Resource newResource,
        bool mergeMetaProfiles = true)
    {
        if (_strategy == null)
        {
            throw new InvalidOperationException("Merge strategy is not set.");
        }
        
        return _strategy.MergeResources(oldResource, newResource, mergeMetaProfiles);
    }
}