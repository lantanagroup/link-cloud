namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

public sealed record SftpCredentialsRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}
