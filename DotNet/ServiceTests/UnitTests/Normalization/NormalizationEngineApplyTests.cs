using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class NormalizationEngineApplyTests
{
    [Fact]
    public async System.Threading.Tasks.Task Apply_copy_location_adds_identifier_as_type_without_removing_existing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNormalizationEngine();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<NormalizationEngine>();

        var location = new Location
        {
            Id = "loc-1",
            Identifier = [new Identifier("http://hospital.example.org/locations", "LOC-1")],
            Type = [new CodeableConcept("https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html", "1027-2", "Medical Ward")]
        };

        await engine.ApplyAllAsync(
            [location],
            [new NormalizationWorkItem(1, new CopyLocationOperation { Name = "copy" }, ["Location"])]);

        location.Type.Should().Contain(t => t.Coding.Any(c => c.Code == "1027-2"));
        location.Type.Should().Contain(t => t.Coding.Any(c => c.System == "http://hospital.example.org/locations" && c.Code == "LOC-1"));
    }
}
