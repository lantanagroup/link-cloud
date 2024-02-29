namespace LantanaGroup.Link.Notification.Domain.Entities
{
    public class FacilityChannel
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            FacilityChannel other = (FacilityChannel)obj;
            return Name == other.Name && Enabled == other.Enabled;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Enabled.GetHashCode();
        }
    }
}
