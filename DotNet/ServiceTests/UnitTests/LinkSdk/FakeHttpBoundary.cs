using System.Text;
using Flurl.Http.Testing;
using Xunit;

namespace UnitTests.LinkSdk;

/// <summary>
/// In-process stand-in for a Link service, built on Flurl's <see cref="HttpTest"/>. Every Flurl call made
/// in the test's async flow is answered with a canned body and recorded, so an SDK client's URL, verb and
/// payload can be asserted without opening a socket.
/// </summary>
/// <remarks>
/// The captured request has the same shape <see cref="OneShotServer"/> produces (path and query split
/// the way <see cref="Uri"/> splits them), so assertions written against one hold against the other.
/// Real HTTP stays disabled: a call that somehow escaped interception would fail rather than reach the
/// network, and <see cref="BaseUrl"/> uses the reserved <c>.test</c> domain in any case.
/// </remarks>
internal sealed class FakeHttpBoundary : IDisposable
{
    private readonly HttpTest _httpTest = new();

    public string BaseUrl => "http://link.test";

    public FakeHttpBoundary(string responseBody, int statusCode = 200)
    {
        _httpTest.RespondWith(() => new StringContent(responseBody, Encoding.UTF8, "application/json"), statusCode);
    }

    /// <summary>
    /// Returns the one request the client made, failing the test if it made none or several.
    /// </summary>
    public CapturedRequest SingleRequest()
    {
        var call = Assert.Single(_httpTest.CallLog);
        var url = new Uri(call.Request.Url.ToString());

        return new CapturedRequest
        {
            Method = call.Request.Verb.Method,
            Path = url.AbsolutePath,
            Query = url.Query,
            Body = call.RequestBody ?? string.Empty
        };
    }

    public void Dispose() => _httpTest.Dispose();
}
