using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class ServiceRequestFactory
{
    /// <summary>Generate a ServiceRequest from the pool using a seed index.</summary>
    public static ServiceRequest Generate(
        string id, string patientId, string encounterId,
        DateTime authored, int seed, string practId,
        List<string> conditionIds)
    {
        var v = FhirGenerationCodes.ServiceRequests[seed % FhirGenerationCodes.ServiceRequests.Length];
        var reasonConditionId = conditionIds.Count > 0
            ? conditionIds[seed % conditionIds.Count]
            : null;
        return Create(id, patientId, encounterId, authored, seed, practId,
                      v.Code, v.Display, v.IsLab, v.System, reasonConditionId);
    }

    /// <summary>Create a ServiceRequest with caller-supplied values.</summary>
    public static ServiceRequest Create(
        string id,
        string patientId,
        string encounterId,
        DateTime authored,
        int seed,
        string practId,
        string code,
        string display,
        bool isLab,
        string codeSystem,
        string? reasonConditionId = null,
        string? organizationId = null)
    {
        var request = new ServiceRequest
        {
            Id = id,
            Status = seed % 4 == 0 ? RequestStatus.Completed : RequestStatus.Active,
            Intent = RequestIntent.Order,
            Category =
            [
                new CodeableConcept
                {
                    Coding = [new Coding("http://snomed.info/sct",
                        isLab ? "108252007" : "409073007",
                        isLab ? "Laboratory procedure" : "Education")],
                    Text = isLab ? "Laboratory" : "Education"
                }
            ],
            Priority = seed % 5 == 0 ? RequestPriority.Urgent : RequestPriority.Routine,
            Code = new CodeableConcept { Coding = [new Coding(codeSystem, code, display)], Text = display },
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Occurrence = new FhirDateTime(authored.AddHours(1)),
            AuthoredOn = authored.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Requester = Ref($"Practitioner/{practId}", "Ordering Physician"),
            Performer = [Ref($"Organization/{organizationId ?? FhirBundleGenerator.HospitalOrgId}", "General Test Hospital")]
        };

        if (reasonConditionId != null)
            request.ReasonReference = [Ref($"Condition/{reasonConditionId}")];

        return request;
    }
}
