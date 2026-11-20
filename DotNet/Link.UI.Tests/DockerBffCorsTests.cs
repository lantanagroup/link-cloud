using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class DockerBffCorsTests
{
    [Fact]
    public void Docker_bff_allows_the_admin_ui_origin_used_by_live_tests()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "DotNet", "Admin.BFF", "appsettings.Docker.json")))
            dir = Path.GetDirectoryName(dir);
        dir.Should().NotBeNull();

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!, "DotNet", "Admin.BFF", "appsettings.Docker.json")));
        var origins = doc.RootElement.GetProperty("CORS").GetProperty("AllowedOrigins").EnumerateArray()
            .Select(origin => origin.GetString())
            .ToList();
        origins.Should().Contain("http://localhost:8066");
    }
}
