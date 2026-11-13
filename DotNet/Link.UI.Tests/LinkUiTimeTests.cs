using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class LinkUiTimeTests
{
    [Fact]
    public void Unspecified_service_timestamps_stay_on_the_utc_clock()
    {
        var stored = new DateTime(2026, 10, 7, 15, 30, 0, DateTimeKind.Unspecified);

        var iso = LinkUiTime.IsoUtc(stored);

        iso.Should().Be("2026-10-07T15:30:00Z");
        LinkUiTime.Title(iso).Should().Be("2026-10-07 15:30:00 UTC");
    }

    [Fact]
    public void Utc_values_keep_the_same_instant()
    {
        var stored = new DateTime(2026, 10, 7, 15, 30, 4, DateTimeKind.Utc);

        LinkUiTime.IsoUtc(stored).Should().Be("2026-10-07T15:30:04Z");
    }

    [Fact]
    public void An_offset_converts_once_to_utc()
    {
        var stored = new DateTimeOffset(2026, 10, 7, 10, 29, 0, TimeSpan.FromHours(-5));

        LinkUiTime.IsoUtc(stored).Should().Be("2026-10-07T15:29:00Z");
        LinkUiTime.Title(LinkUiTime.IsoUtc(stored)).Should().Be("2026-10-07 15:29:00 UTC");
    }

    [Fact]
    public void A_local_value_converts_once_and_is_not_shifted_again()
    {
        var local = new DateTime(2026, 10, 7, 10, 29, 0, DateTimeKind.Local);
        var once = local.ToUniversalTime();

        LinkUiTime.IsoUtc(local).Should().Be(LinkUiTime.IsoUtc(once));
        LinkUiTime.IsoUtc(LinkUiTime.IsoUtc(local) is var iso
            ? DateTime.SpecifyKind(
                DateTime.ParseExact(iso, "yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
                DateTimeKind.Utc)
            : default).Should().Be(iso);
    }

    [Fact]
    public void Empty_values_stay_empty()
    {
        LinkUiTime.IsoUtc(default(DateTime)).Should().BeEmpty();
        LinkUiTime.IsoUtc((DateTime?)null).Should().BeEmpty();
        LinkUiTime.IsoUtc(default(DateTimeOffset)).Should().BeEmpty();
        LinkUiTime.IsoUtc((DateTimeOffset?)null).Should().BeEmpty();
        LinkUiTime.Title(null).Should().BeEmpty();
        LinkUiTime.Title("10/7/26 10:29 AM").Should().Be("10/7/26 10:29 AM");
    }

    [Fact]
    public void Display_is_the_labeled_utc_text_the_browser_converts()
    {
        var stored = new DateTime(2026, 10, 7, 15, 30, 0, DateTimeKind.Unspecified);
        var offset = new DateTimeOffset(2026, 10, 7, 10, 29, 0, TimeSpan.FromHours(-5));

        LinkUiTime.Display(stored).Should().Be(LinkUiTime.Title(LinkUiTime.IsoUtc(stored)));
        LinkUiTime.Display(stored).Should().Be("2026-10-07 15:30:00 UTC");
        LinkUiTime.Display(offset).Should().Be("2026-10-07 15:29:00 UTC");
        LinkUiTime.Display((DateTime?)stored).Should().Be("2026-10-07 15:30:00 UTC");
        LinkUiTime.Display((DateTime?)null).Should().BeEmpty();
        LinkUiTime.Display(default(DateTime)).Should().BeEmpty();
        LinkUiTime.Display(default(DateTimeOffset)).Should().BeEmpty();
        LinkUiTime.Display((DateTimeOffset?)null).Should().BeEmpty();
    }

    [Fact]
    public void Link_ui_does_not_read_the_server_local_clock()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        var root = Path.Combine(dir!.FullName, "DotNet", "Link.UI");
        var hits = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") == false
                && path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") == false)
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("DateTime.Now", StringComparison.Ordinal)
                    || text.Contains("DateTime.Today", StringComparison.Ordinal)
                    || text.Contains(".ToLocalTime(", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        hits.Should().BeEmpty();
    }
}
