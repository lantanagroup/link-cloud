namespace LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token
{
    /// <summary>
    /// LEGLINK-1359: the person on whose behalf the current call is being made, when there is one.
    /// </summary>
    /// <remarks>
    /// Registered only by a host that fronts real users - today the NHSN App BFF, which authenticates
    /// the caller with a gateway JWT and then calls Link services with a token of its own, so without
    /// this the caller's identity stops at the BFF. Nothing registers it inside a Link service or
    /// worker, and <see cref="ICreateSystemToken"/> takes it as an optional dependency, so those hosts
    /// keep minting exactly the token they mint today.
    /// </remarks>
    public interface IActorEmailProvider
    {
        /// <summary>
        /// The caller's email address, or null when the call cannot be attributed to a person: no
        /// request in scope, an unauthenticated request, or a token carrying no email claim.
        /// </summary>
        /// <remarks>
        /// Read once per minted token, which is once per outbound call, so an implementation must be
        /// cheap and must not throw - a call that cannot be attributed still has to be able to mint a
        /// token.
        /// </remarks>
        string? CurrentEmail { get; }
    }
}
