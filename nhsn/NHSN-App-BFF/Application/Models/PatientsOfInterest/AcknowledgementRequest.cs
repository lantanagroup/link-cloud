namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

public class AcknowledgementRequest
{
    public required bool Accepted { get; set; }
    public required string StatementKey { get; set; }
}
