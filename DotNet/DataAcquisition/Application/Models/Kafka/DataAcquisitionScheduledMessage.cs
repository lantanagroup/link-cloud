namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

public class DataAcquisitionScheduledMessage : IBaseDataAcqMessage
{
    public string CorrelationId { get; set; }
    public bool Bulk { get; set; }
    public List<DataAcquisitionTypes> Type { get; set; }
    public string ReportMonth { get; set; }
    public string ReportYear { get; set; }
    public string TenantId { get; set; }
    public string FacilityId { get; set; }
    public BaseData BaseData { get; set; }
}

public partial class BaseData { }

public class PatientList : BaseData
{
    public IEnumerable<PatientOfInterest> Patients { get; set; }
}

public enum DataAcquisitionTypes
{
    PatientList,
    Patient,
    Encounter,
    Condition,
    MedicationRequest,
    Observation,
    Procedure,
    ServiceRequest,
    Coverage,
    MedicationAdministration
}
