using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Configuration;
using Link.UI.Controllers;
using Link.UI.Engine.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

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

    [Fact]
    public void Index_MissingFhirUrl_ShowsNotConfigured()
    {
        var sut = Create(null);

        var result = sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AboutViewModel>().Subject;

        model.FhirServerHostname.Should().Be("Not configured");
        model.FhirServerBaseUrl.Should().BeNull();
    }

    [Fact]
    public void Index_NonAbsoluteUrl_ShowsNotConfiguredHostname()
    {
        var sut = Create("not-a-url");

        var result = sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AboutViewModel>().Subject;

        model.FhirServerHostname.Should().Be("Not configured");
        model.FhirServerBaseUrl.Should().Be("not-a-url");
    }

    private static AboutController Create(string? fhirServerBase) =>
        new(new FixedOptions(new AutomationConfig { FhirServerBase = fhirServerBase! }));

    private sealed class FixedOptions : IOptionsMonitor<AutomationConfig>
    {
        public FixedOptions(AutomationConfig value) => CurrentValue = value;
        public AutomationConfig CurrentValue { get; }
        public AutomationConfig Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AutomationConfig, string?> listener) => null;
    }
}
