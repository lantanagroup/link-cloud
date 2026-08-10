namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// Answers whether a facility exists, so reporting plans can be validated against it. DMRP cannot
    /// reference the host's facility entity -- the host references DMRP, not the other way round -- so
    /// the module declares the question here and the host answers it. The host must register an
    /// implementation; DMRP deliberately provides no default.
    /// </summary>
    public interface IFacilityExistence
    {
        Task<bool> ExistsAsync(string facilityId, CancellationToken cancellationToken = default);
    }
}
