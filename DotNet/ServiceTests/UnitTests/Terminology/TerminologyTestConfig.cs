using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using Microsoft.Extensions.Options;

namespace UnitTests.Terminology;

/// <summary>
/// Builds the <see cref="TerminologyConfig"/> the Terminology services need injected.
/// </summary>
/// <remarks>
/// <see cref="FhirService"/> reads its expansion bounds from configuration (LEGLINK-968), so every test
/// that constructs one has to supply them. Defaulting to the production values keeps tests that do not
/// care about paging reading as though the parameter were not there, while a test that does care passes
/// small numbers so a page boundary is reachable with a handful of codes.
/// <see cref="TerminologyConfig.Path"/> is <c>required</c> and must always be set, even though nothing
/// in these tests reads from disk.
/// </remarks>
internal static class TerminologyTestConfig
{
    internal static IOptions<TerminologyConfig> Options(
        int defaultExpansionPageSize = ExpansionDefaults.DefaultPageSize,
        int maxExpansionPageSize = ExpansionDefaults.MaxPageSize,
        bool enableCodeUploadEndpoint = false) =>
        Microsoft.Extensions.Options.Options.Create(new TerminologyConfig
        {
            Path = "/test/path",
            EnableCodeUploadEndpoint = enableCodeUploadEndpoint,
            DefaultExpansionPageSize = defaultExpansionPageSize,
            MaxExpansionPageSize = maxExpansionPageSize
        });
}
