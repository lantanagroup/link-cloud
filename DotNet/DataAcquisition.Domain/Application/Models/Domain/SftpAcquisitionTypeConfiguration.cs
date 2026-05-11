using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

/// <summary>
/// Configuration for a specific acquisition type within an SFTP connection.
/// Allows a facility to have multiple acquisition types (census, resources, etc.)
/// from the same SFTP server.
/// </summary>
public class SftpAcquisitionTypeConfiguration
{
    /// <summary>
    /// The type of acquisition this configuration handles.
    /// </summary>
    public SftpAcquisitionType AcquisitionType { get; set; }

    /// <summary>
    /// The subtype of acquisition, used to select the specific processor/parser
    /// within a broad acquisition type (e.g., CernerCCLExtract within Census).
    /// </summary>
    public SftpAcquisitionSubType SubType { get; set; } = SftpAcquisitionSubType.None;

    /// <summary>
    /// Remote directory for this acquisition type.
    /// If null, uses the parent SftpConfiguration.RemoteDirectory.
    /// </summary>
    public string? RemoteDirectory { get; set; }

    /// <summary>
    /// Directory to move files to after successful processing.
    /// Files are moved here instead of being deleted, allowing for audit/recovery.
    /// If null, files are deleted (when RemoveAfterProcessing is true) or left in place.
    /// Example: "/processed" or "/archive/census"
    /// </summary>
    public string? ProcessedDirectory { get; set; }

    /// <summary>
    /// File name pattern to match. Supports wildcards (*).
    /// Examples: "lantana_census_extract_*.dat", "*.json", "resources_*.xml"
    /// If null/empty, all files in the directory are considered.
    /// </summary>
    public string? FileNamePattern { get; set; }

    /// <summary>
    /// Optional parsing configuration for this acquisition type.
    /// If null, uses the default parser based on AcquisitionType and file extension.
    /// </summary>
    public FileParsingConfiguration? ParsingConfiguration { get; set; }
}
