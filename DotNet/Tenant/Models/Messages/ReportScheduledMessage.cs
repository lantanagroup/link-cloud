namespace LantanaGroup.Link.Tenant.Models.Messages;

public class ReportScheduledMessage
{
    public string[] ReportTypes { get; set; }
    public string Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

}
