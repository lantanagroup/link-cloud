using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Application.Factories;

public class PatientEncounterBuilder
{
    private PatientEncounter _patientEncounter = new PatientEncounter();

    public PatientEncounterBuilder(string facilityId, string MRN, DateTime admitDate, DateTime? dischargeDate, string correlationId)
    {
        _patientEncounter.FacilityId = facilityId;
        _patientEncounter.MedicalRecordNumber = MRN;
        _patientEncounter.AdmitDate = admitDate;

        if (dischargeDate != default)
        {
            _patientEncounter.DischargeDate = dischargeDate;
        }

        _patientEncounter.CorrelationId = correlationId;
        _patientEncounter.CreateDate = DateTime.Now;
    }

    public PatientEncounterBuilder AddVisitIdentifier(string visitId, SourceType sourceType)
    {
        PatientVisitIdentifier identifier = new PatientVisitIdentifier() { Identifier = visitId, SourceType = sourceType.ToString(), PatientEncounterId = _patientEncounter.Id };

        if (_patientEncounter.PatientVisitIdentifiers == null)
        {
            _patientEncounter.PatientVisitIdentifiers = new List<PatientVisitIdentifier>();
        }

        _patientEncounter.PatientVisitIdentifiers.Add(identifier);

        return this;
    }

    public PatientEncounterBuilder AddPatientIdentifier(string patientId, SourceType sourceType)
    {
        PatientIdentifier identifier = new PatientIdentifier() { Identifier = patientId, SourceType = sourceType.ToString() };

        if (_patientEncounter.PatientIdentifiers == null)
        {
            _patientEncounter.PatientIdentifiers = new List<PatientIdentifier>();
        }

        _patientEncounter.PatientIdentifiers.Add(identifier);

        return this;
    }

    public PatientEncounter GetPatientEncounter()
    {
        return _patientEncounter;
    }
}
