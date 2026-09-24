namespace QueryDispatch.Application.Interfaces
{
    public interface IQueryDispatchServiceMetrics
    {
        void IncrementPatientsDispatched(string facilityId, string outcome);
        void RecordDispatchDuration(string facilityId, double durationMilliseconds);
    }
}
