namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security
{
    public class Group
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public List<string> FacilityIds { get; set; } = [];
        public List<Role> Roles { get; set; } = [];
    }
}
