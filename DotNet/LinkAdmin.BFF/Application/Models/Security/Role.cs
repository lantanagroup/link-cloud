namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security
{
    public class Role
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public List<string> FacilityIds { get; set; } = [];
    }
}
