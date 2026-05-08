using Microsoft.AspNetCore.Authentication;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication.CdcSams
{
    public static class SamsAuthenticationOptionsExtensions
    {
        /// <summary>
        /// Add CDC SAMS authentication to the specified <see cref="AuthenticationBuilder"/>, which enables CDC SAMS authentication capabilities.
        /// The default scheme is set to <see cref="SamsDefaults.AuthenticationScheme"/>.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <returns>A reference to <paramref name="builder"/> after the operation as completed.</returns>
        public static AuthenticationBuilder AddCdcSams(this AuthenticationBuilder builder)
            => builder.AddCdcSams(SamsDefaults.AuthenticationScheme, _ => { });

        /// <summary>
        /// Add CDC SAMS authentication to the specified <see cref="AuthenticationBuilder"/>, which enables CDC SAMS authentication capabilities.
        /// The default scheme is set to <see cref="SamsDefaults.AuthenticationScheme"/>.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <param name="configureOptions">A delegate to configure <see cref="SamsOptions"/>.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddCdcSams(this AuthenticationBuilder builder, Action<SamsOptions> configureOptions)
            => builder.AddCdcSams(SamsDefaults.AuthenticationScheme, configureOptions => { });

        /// <summary>
        /// Add CDC SAMS authentication to the specified <see cref="AuthenticationBuilder"/>, which enables CDC SAMS authentication capabilities.
        /// The default scheme is set to <see cref="SamsDefaults.AuthenticationScheme"/>.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <param name="configureOptions">A delegate to configure <see cref="SamsOptions"/>.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddCdcSams(this AuthenticationBuilder builder, string authenticationScheme, Action<SamsOptions> configureOptions)
            => builder.AddCdcSams(authenticationScheme, SamsDefaults.DisplayName, configureOptions);

        /// <summary>
        /// Add CDC SAMS authentication to the specified <see cref="AuthenticationBuilder"/>, which enables CDC SAMS authentication capabilities.
        /// The default scheme is set to <see cref="SamsDefaults.AuthenticationScheme"/>.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <param name="displayName">A display name for the authentication handler</param>
        /// <param name="configureOptions">A delegate to configure <see cref="SamsOptions"/>.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddCdcSams(this AuthenticationBuilder builder, string authenticationScheme, string displayName, Action<SamsOptions> configureOptions)
            => builder.AddOAuth<SamsOptions, SamsHandler>(authenticationScheme, displayName, configureOptions);
      
    }
}
