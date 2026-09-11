namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

public class ConnectionResult
{
    public required bool Success { get; set; }
    public required string MessageKey { get; set; }
    public string? Detail { get; set; }
    public bool Simulated { get; set; }
}
