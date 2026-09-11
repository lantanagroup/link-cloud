namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class RemoveExtensionsOperation : IOperation
    {
        public OperationType OperationType => OperationType.RemoveExtensions;

        public string Name { get; set; }
        public string Description { get; set; }

        // URLs of top-level extensions to remove; extensions with URLs not in this list are left intact.
        public List<string> ExtensionUrls { get; set; } = new();
    }
}
