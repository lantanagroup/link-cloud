using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;

public class UseLatestStrategy(ILogger<UseLatestStrategy> logger) : IResourceMergeStrategy
{
    public Resource MergeResources(Resource oldResource, Resource newResource)
    {
        ArgumentNullException.ThrowIfNull(oldResource);
        ArgumentNullException.ThrowIfNull(newResource);
        
        if (oldResource.Id != newResource.Id)
        {
            logger.LogWarning("Merging resources with mismatched IDs: old={OldId}, new={NewId}", oldResource.Id, newResource.Id);
        }
        
        //TODO: add property to control whether to merge or replace profiles
        // combine the meta profiles
        var existingProfiles = oldResource.Meta?.Profile.ToList() ?? [];
        var newProfiles = newResource.Meta?.Profile.ToList() ?? [];
                                            
        var profileSet = new HashSet<string>(existingProfiles);
        profileSet.UnionWith(newProfiles);
                                            
        logger.LogInformation("Combining meta profiles for resource {ResourceId} with existing profiles: [{ExistingProfiles}] and new profiles: [{NewProfiles}].",
            newResource.Id, string.Join(", ", existingProfiles), string.Join(", ", newProfiles));
        
        newResource.Meta = new Meta { Profile = profileSet.ToList() };
        
        logger.LogInformation("Updated resource {ResourceId} with new acquired instance.", oldResource.Id);

        return newResource;

    }
}