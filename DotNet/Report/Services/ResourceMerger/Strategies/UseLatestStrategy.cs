using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;

public class UseLatestStrategy(ILogger<UseLatestStrategy> logger) : IResourceMergeStrategy
{
    /// <summary>
    /// Merges two FHIR resources that are of the same type, replacing the old resource with the new one.
    /// Merge is done by combining the meta profiles if specified via the `mergeMetaProfiles` parameter.
    /// If the resources are of different types, the initial resource is returned without merging.
    /// </summary>
    /// <param name="oldResource"></param>
    /// <param name="newResource"></param>
    /// <param name="mergeMetaProfiles"></param>
    /// <returns></returns>
    public Resource MergeResources(Resource oldResource, Resource newResource, bool mergeMetaProfiles = true)
    {
        ArgumentNullException.ThrowIfNull(oldResource);
        ArgumentNullException.ThrowIfNull(newResource);
        
        // Ensure both resources are of the same type
        if (oldResource.GetType() != newResource.GetType())
        {
            logger.LogError("Cannot merge resources of different types: {oldResource} and {newResource}. Returning initial resource without merging.", oldResource.GetType().Name, newResource.GetType().Name);
            return oldResource;
        }
        
        // Ensure both resources have the same FHIR ID
        if (oldResource.Id != newResource.Id)
        {
            logger.LogError("Cannot merge resources with mismatched IDs: old={OldId}, new={NewId}", oldResource.Id, newResource.Id);
            return oldResource;
        }
        
        if(mergeMetaProfiles)
        {
            // combine the meta profiles
            var existingProfiles = oldResource.Meta?.Profile.ToList() ?? [];
            var newProfiles = newResource.Meta?.Profile.ToList() ?? [];
                                            
            var profileSet = new HashSet<string>(existingProfiles);
            profileSet.UnionWith(newProfiles);
            
            logger.LogInformation("Combining meta profiles for resource {ResourceId} with existing profiles: [{ExistingProfiles}] and new profiles: [{NewProfiles}].",
                newResource.Id, string.Join(", ", existingProfiles), string.Join(", ", newProfiles));
        
            newResource.Meta ??= new Meta();
            newResource.Meta.Profile = profileSet.ToList();
        }
        
        logger.LogInformation("Updated resource {ResourceId} with new acquired instance.", oldResource.Id);

        return newResource;

    }
}