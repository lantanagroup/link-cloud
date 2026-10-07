namespace UnitTests.LinkSdk;

/// <summary>
/// A request seen by <see cref="DataAcquisitionRouteProbeServer"/>, with the controller action routing matched.
/// </summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The request path, without the query string.</param>
/// <param name="MatchedAction">The matched action as <c>Controller.Action</c>, or null when nothing matched.</param>
internal sealed record ProbedRequest(string Method, string Path, string? MatchedAction);
