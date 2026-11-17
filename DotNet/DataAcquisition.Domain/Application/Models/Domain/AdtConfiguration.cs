namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

/// <summary>
/// How a facility's HL7 v2 ADT census feed identifies patients. Stored as JSON with its
/// <see cref="SftpAcquisitionTypeConfiguration"/>, so changing it needs no migration.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="PatientIdLocation"/> and <see cref="MrnIdentifierSystem"/> must be set. Locations use the
/// syntax described on <see cref="Services.Sftp.Hl7.Hl7Path"/>.
/// </remarks>
public class AdtConfiguration
{
    /// <summary>The MRN location used when <see cref="MrnLocation"/> is not set: the PID-3 repetition with type MR.</summary>
    public const string DefaultMrnLocation = "PID-3[5=MR].1";

    /// <summary>The encounter id location used when <see cref="EncounterIdLocation"/> is not set: PV1-19, visit number.</summary>
    public const string DefaultEncounterIdLocation = "PV1-19.1";

    /// <summary>
    /// Where the ADT message carries the FHIR Patient.Id, such as <c>PID-3[5=FHIR].1</c> or <c>OBX[3.1=FHIRID]-5</c>.
    /// </summary>
    public string? PatientIdLocation { get; set; }

    /// <summary>
    /// The identifier system of the MRN on the FHIR server. When set, the Patient.Id is found with
    /// <c>GET /Patient?identifier=&lt;system&gt;|&lt;mrn&gt;</c>.
    /// </summary>
    public string? MrnIdentifierSystem { get; set; }

    /// <summary>Overrides where the MRN is read from. Defaults to <see cref="DefaultMrnLocation"/>.</summary>
    public string? MrnLocation { get; set; }

    /// <summary>Overrides where the encounter id is read from. Defaults to <see cref="DefaultEncounterIdLocation"/>.</summary>
    public string? EncounterIdLocation { get; set; }
}
