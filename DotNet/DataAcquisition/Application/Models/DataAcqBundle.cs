namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class DataAcqBundle
{
    public string ResourceType { get; set; } = "Bundle";
    public string Id { get; set; } = "bundle";
    public Meta Meta { get; set; } = new Meta();
    public string Type { get; set; } = "collection";
    public List<Entry> Entry { get; set; } = new List<Entry>();
}

public class Meta
{
    public string LastUpdated { get; set; } = DateTime.Now.ToString("yyyy-MM-ddThh:mm:ssZ");
}

public class Entry
{
    public string FullUrl { get; set; }
    public object Resource { set; get; }
}

