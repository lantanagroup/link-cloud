namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// Answers whether a facility exists. The host must register an implementation; the module
    /// provides no default.
    /// </summary>
    public interface IFacilityExistence
    {
        Task<bool> ExistsAsync(string facilityId, CancellationToken cancellationToken = default);
    }
}
