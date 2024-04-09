namespace LantanaGroup.Link.MeasureEval.Models
{
    public class PackageDefinition
    {
        public string Name { get; set; } = null!;
        public List<string> BundleIds { get; set; } = new List<string>();
    }
}
