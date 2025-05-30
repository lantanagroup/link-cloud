using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Application.ResourceCategories
{
    public static class ResourceCategory
    {
        private static List<string> SharedResources()
        {
            return
            [
                nameof(Location),
                nameof(Medication)
            ];
        }

        public static ResourceCategoryType? GetResourceCategoryByType(string typeName)
        {
            //Return null if the incoming type is not FHIR related
            if (!Enum.GetNames(typeof(FHIRDefinedType)).OfType<string>().ToList().Any(x => x == typeName)) 
            {
                return null;
            }

            if (SharedResources().Any(x => x == typeName))
            {
                return ResourceCategoryType.Shared;
            }

            //TODO: Daniel - Potentially dangerous if we didn't add a shared resource to the SharedResources list.
            return ResourceCategoryType.Patient;
        }
    }
}

