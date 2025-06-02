namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    public class PostOperationSequences
    {
        public required string ResourceType { get; set; }
        public List<PostOperationSequence> OperationSequences { get; set; }  = new List<PostOperationSequence>();
    }

    public class PostOperationSequence
    {
        public required Guid OperationId { get; set; } 
        public required int Sequence { get; set; } 
        public Guid? VendorPresetId { get; set; }
    }
}
