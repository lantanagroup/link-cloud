using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

// Wire models for POST api/data/sftp-configurations/test-connection. They mirror the Data Acquisition
// models of the same shape (SftpTestConnectionRequestModel and friends) by hand, so keep the two in step.

/// <summary>
/// SFTP connection details to test. Nothing in the request is stored.
/// </summary>
public class SftpTestConnectionRequestApiModel
{
    /// <summary>
    /// The SFTP host name or IP address, without a scheme.
    /// </summary>
    [JsonPropertyName("hostName")]
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// The SFTP port.
    /// </summary>
    [JsonPropertyName("hostUrlPort")]
    public int HostUrlPort { get; set; } = 22;

    /// <summary>
    /// Username for SFTP authentication.
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password for SFTP authentication.
    /// </summary>
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The directory on the SFTP server to read.
    /// </summary>
    [JsonPropertyName("reportDirectory")]
    public string ReportDirectory { get; set; } = "/";
}

/// <summary>
/// Result of an ad-hoc SFTP connection test.
/// </summary>
public class SftpTestConnectionResultApiModel
{
    /// <summary>
    /// Indicates whether the connection test was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// A message describing the result of the connection test.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Files found in reportDirectory. Only populated when includeFileContent=true is passed.
    /// </summary>
    [JsonPropertyName("files")]
    public List<SftpTestFileApiModel>? Files { get; set; }
}

/// <summary>
/// A file found in the report directory, with a preview of the patients it contains.
/// </summary>
public class SftpTestFileApiModel
{
    /// <summary>
    /// Name of the file.
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Patients previewed from the file. Empty when the file is not a Cerner extract or was not previewed.
    /// </summary>
    [JsonPropertyName("patients")]
    public List<SftpTestFilePatientApiModel> Patients { get; set; } = [];
}

/// <summary>
/// A patient previewed from a Cerner extract.
/// </summary>
public class SftpTestFilePatientApiModel
{
    /// <summary>
    /// Patient id.
    /// </summary>
    [JsonPropertyName("patientId")]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient name as it appears in the extract.
    /// </summary>
    [JsonPropertyName("patientName")]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Admission date from the patient's first encounter row, when it could be read.
    /// </summary>
    [JsonPropertyName("admissionDate")]
    public DateTime? AdmissionDate { get; set; }
}
