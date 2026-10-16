
using Automation.UI.Controllers;
using Automation.UI.Models;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AboutControllerTests
{
    [Fact]
    public void Index_ValidLocalFhirUrl_DisplaysHostnameAndBaseUrl()
    {
        var sut = Create("http://localhost:6157/fhir");

        var result = sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AboutViewModel>().Subject;

        model.FhirServerHostname.Should().Be("localhost");
        model.FhirServerBaseUrl.Should().Be("http://localhost:6157/fhir");
    }

    [Fact]
    public void Index_ValidRemoteFhirUrl_DisplaysHostnameAndBaseUrl()
    {
        var sut = Create("https://fhir-test.example.org:8443/fhir");

        var result = sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AboutViewModel>().Subject;

        model.FhirServerHostname.Should().Be("fhir-test.example.org");
        model.FhirServerBaseUrl.Should().Be("https://fhir-test.example.org:8443/fhir");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Index_MissingFhirUrl_DisplaysNotConfigured(string? fhirUrl)
    {
        var sut = Create(fhirUrl);

        var result = sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AboutViewModel>().Subject;

        model.FhirServerHostname.Should().Be("Not configured");
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://fhir.example.org/fhir")]
    [InlineData("localhost:6157/fhir")]
    public void Index_InvalidFhirUrl_DisplaysNotConfigured(string fhirUrl)
    {
        var sut = Create(fhirUrl);

        var result = sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AboutViewModel>().Subject;

        model.FhirServerHostname.Should().Be("Not configured");
        model.FhirServerBaseUrl.Should().Be(fhirUrl);
    }

    private static AboutController Create(string? fhirUrl)
    {
        var config = new AutomationConfig
        {
            FhirServerBase = fhirUrl!
        };

        var options = new Mock<IOptionsMonitor<AutomationConfig>>();
        options.SetupGet(o => o.CurrentValue).Returns(config);

        return new AboutController(options.Object);
    }
}
