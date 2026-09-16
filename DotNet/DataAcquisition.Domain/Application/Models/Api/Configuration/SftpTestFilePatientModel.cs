namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

/// <summary>
/// Represents a patient model for SFTP test file operations.
/// </summary>
public record SftpTestFilePatientModel
{
    /// <summary>
    /// Patient ID associated with a patient inthe test file.
    /// </summary>
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient name associated with a patient in the test file.
    /// </summary>
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Admission date associated with a patient in the test file.
    /// If the admission date is not available, this property can be null.
    /// </summary>
    public DateTime? AdmissionDate { get; set; } = null;
}