namespace LantanaGroup.Link.Submission.Application.Interfaces
{
    public interface ISubmissionServiceMetrics
    {
        void IncrementReportSubmittedCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementResourcesSubmittedCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementResourceTypeCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementMedicationCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementEncounterCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementLocationCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementDiagnosticCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementObservationCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementMedicationRequestCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementSpecimenCounter(int count, List<KeyValuePair<string, object?>> tags);
        void IncrementServiceRequestCounter(int count, List<KeyValuePair<string, object?>> tags);
    }
}
