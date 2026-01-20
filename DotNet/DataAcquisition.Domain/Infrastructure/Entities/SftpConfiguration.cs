using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("SftpConfiguration")]
public class SftpConfiguration
{
    /// <summary>
    /// Unique identifier for the SFTP configuration.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    ///  Identifier for the organization associated with this SFTP configuration.
    /// </summary>
    public string OrganizationId { get; set; } = null!;
    
    /// <summary>
    /// Hostname or IP address of the SFTP server.
    /// </summary>
    public string Host { get; set; } = null!;
    
    /// <summary>
    /// Port number for the SFTP server. Defaults to standard SFTP port 22.
    /// </summary>
    public int Port { get; set; } = 22; // Default SFTP port
    
    /// <summary>
    /// Path to the remote directory on the SFTP server.
    /// </summary>
    public string? RemoteDirectory { get; set; } //path to pull files from
    
    /// <summary>
    /// How long to wait for SFTP operations before timing out.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
    
    /// <summary>
    /// Indicates whether to remove files from the SFTP server after processing. Defaults to false.
    /// </summary>
    public bool RemoveAfterProcessing { get; set; }

    /// <summary>
    /// Authentication type for SFTP connection. If no type is specified, defaults to "Basic".
    /// Current supported types: "Basic"
    /// </summary>
    public AuthType AuthenticationProtocol { get; set; } = AuthType.Basic;
}