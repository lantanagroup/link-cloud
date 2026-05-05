using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class CarePlanFactory
{
    /// <summary>Generate a CarePlan from the activity pool using a seed index.</summary>
    public static CarePlan Generate(
        string id, string patientId, string encounterId,
        string careTeamId, DateTime period, int seed)
    {
        var a = FhirGenerationCodes.CarePlanActivities[seed % FhirGenerationCodes.CarePlanActivities.Length];
        return Create(id, patientId, encounterId, careTeamId, period,
                      a.Code, a.Display, a.GoalCode, a.GoalDisplay);
    }

    /// <summary>Create a CarePlan with caller-supplied activity values.</summary>
    public static CarePlan Create(
        string id, string patientId, string encounterId,
        string careTeamId, DateTime period,
        string activityCode, string activityDisplay,
        string goalCode, string goalDisplay) => new()
        {
            Id = id,
            Status = RequestStatus.Active,
            Intent = CarePlan.CarePlanIntent.Order,
            Title = $"Inpatient Care Plan",
            Description = $"Inpatient care plan for {activityDisplay.ToLower()} and recovery.",
            Category =
        [
            CC("http://hl7.org/fhir/us/core/CodeSystem/careplan-category", "assess-plan", "Assessment and Plan of Treatment")
        ],
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Period = new Period { StartElement = new FhirDateTime(period) },
            CareTeam = [Ref($"CareTeam/{careTeamId}")],
            Goal =
        [
            new ResourceReference
            {
                Display = goalDisplay
            }
        ],
            Activity =
        [
            new CarePlan.ActivityComponent
            {
                Detail = new CarePlan.DetailComponent
                {
                    Code        = Snomed(activityCode, activityDisplay),
                    Status      = CarePlan.CarePlanActivityStatus.InProgress,
                    Description = $"Patient receiving {activityDisplay.ToLower()} as part of inpatient management.",
                    Scheduled   = new Timing
                    {
                        Repeat = new Timing.RepeatComponent
                        {
                            Frequency  = 1,
                            Period     = 1,
                            PeriodUnit = Timing.UnitsOfTime.D
                        }
                    }
                }
            }
        ]
        };
}
