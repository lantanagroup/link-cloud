using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;

[DataContract]
public class PutHSLOCModel
{
    [Required]
    [DataMember]
    public string OldVersion { get; set; } = string.Empty;

    [Required]
    [DataMember]    
    public string NewVersion { get; set; } = string.Empty;

    [Required]
    [DataMember]
    public IFormFile CsvFile { get; set; } = null!;
}