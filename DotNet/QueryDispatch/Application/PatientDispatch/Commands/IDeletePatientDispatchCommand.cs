namespace LantanaGroup.Link.QueryDispatch.Application.PatientDispatch.Commands
{
    public interface IDeletePatientDispatchCommand
    {
        Task<bool> Execute(string facilityId, string patientId);
    }
}
