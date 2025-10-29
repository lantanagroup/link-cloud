using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Models;

namespace LantanaGroup.Link.Census.Application.Factories;

public class PatientEncounterBuilder
{
    private PatientEncounterModel _patientEncounter = new PatientEncounterModel();

    public PatientEncounterBuilder(string facilityId, string MRN, DateTime admitDate, DateTime? dischargeDate, string correlationId)
    {
        _patientEncounter.FacilityId = facilityId;
        _patientEncounter.MedicalRecordNumber = MRN;
        _patientEncounter.AdmitDate = admitDate;

        if (dischargeDate != null)
        {
            _patientEncounter.DischargeDate = dischargeDate;
        }

        _patientEncounter.CorrelationId = correlationId;
        _patientEncounter.CreateDate = DateTime.Now;
    }

    public PatientEncounterBuilder AddVisitIdentifier(string visitId, SourceType sourceType)
    {
        var identifier = new PatientVisitIdentifierModel() { Identifier = visitId, SourceType = sourceType.ToString(), PatientEncounterId = _patientEncounter.Id };

        if (_patientEncounter.PatientVisitIdentifiers == null)
        {
            _patientEncounter.PatientVisitIdentifiers = new List<PatientVisitIdentifierModel>();
        }

        _patientEncounter.PatientVisitIdentifiers.Add(identifier);

        return this;
    }

    public PatientEncounterBuilder AddPatientIdentifier(string patientId, SourceType sourceType)
    {
        var identifier = new PatientIdentifierModel() { Identifier = patientId, SourceType = sourceType.ToString() };

        if (_patientEncounter.PatientIdentifiers == null)
        {
            _patientEncounter.PatientIdentifiers = new List<PatientIdentifierModel>();
        }

        _patientEncounter.PatientIdentifiers.Add(identifier);

        return this;
    }

    public PatientEncounterModel GetPatientEncounter()
    {
        return _patientEncounter;
    }
}
