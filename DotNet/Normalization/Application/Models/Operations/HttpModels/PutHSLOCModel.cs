using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;

public class PutHSLOCModel
{
    [Required]
    public string OldVersion { get; set; } = string.Empty;

    [Required]
    public string NewVersion { get; set; } = string.Empty;

    [Required]
    public IFormFile CsvFile { get; set; } = null!;
}