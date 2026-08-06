using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class ServiceRegistryTests
{
    [Fact]
    public void AdminBffServiceApiUrl_returns_null_when_source_is_missing()
    {
        var sut = new ServiceRegistry();

        sut.AdminBffServiceApiUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("http://admin-bff:8063", "http://admin-bff:8063/api")]
    [InlineData("http://admin-bff:8063/", "http://admin-bff:8063/api")]
    [InlineData("http://admin-bff:8063/api", "http://admin-bff:8063/api")]
    [InlineData("http://admin-bff:8063/api/", "http://admin-bff:8063/api")]
    public void AdminBffServiceApiUrl_normalizes_expected_shapes(string input, string expected)
    {
        var sut = new ServiceRegistry
        {
            AdminBffServiceUrl = input
        };

        sut.AdminBffServiceApiUrl.Should().Be(expected);
    }

    [Fact]
    public void PublicAdminBffServiceApiUrl_returns_null_when_source_is_missing()
    {
        var sut = new ServiceRegistry();

        sut.PublicAdminBffServiceApiUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("https://admin.example.org", "https://admin.example.org/api")]
    [InlineData("https://admin.example.org/", "https://admin.example.org/api")]
    [InlineData("https://admin.example.org/api", "https://admin.example.org/api")]
    [InlineData("https://admin.example.org/api/", "https://admin.example.org/api")]
    public void PublicAdminBffServiceApiUrl_normalizes_expected_shapes(string input, string expected)
    {
        var sut = new ServiceRegistry
        {
            PublicAdminBffServiceUrl = input
        };

        sut.PublicAdminBffServiceApiUrl.Should().Be(expected);
    }
}
