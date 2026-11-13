namespace Link.AspireAppHost.Security;

/// <summary>How people sign in to the Admin UI under this AppHost.</summary>
internal enum AuthMode
{
    /// <summary>Every service runs with anonymous access, and the Admin UI asks no one to sign in. The default, and the http launch profile.</summary>
    Anonymous,

    /// <summary>Keycloak signs people in to the Admin BFF over OpenID Connect. The https launch profile.</summary>
    Keycloak
}
