using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;

public interface IResourceMergeStrategy
{
    Resource MergeResources(
        Resource oldResource,
        Resource newResource,
        bool mergeMetaProfiles = true);
}