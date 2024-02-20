namespace LantanaGroup.Link.Notification.Application.Interfaces.Clients
{
    public interface IFacilityClient
    {
        Task<HttpResponseMessage> VerifyFacilityExists(string facilityId);
    }
}
