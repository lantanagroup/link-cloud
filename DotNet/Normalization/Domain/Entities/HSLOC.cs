using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[Table("HSLOC")]
public partial class HSLOC
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HSLOCCode { get; set; } = "";
    public string CDCCode { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string LongDescription { get; set; } = "";
    public string Version { get; set; } = "";
    public bool IsActive { get; set; } = true;

}