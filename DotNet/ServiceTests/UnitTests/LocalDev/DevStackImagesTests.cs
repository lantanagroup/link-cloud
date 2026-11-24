using FluentAssertions;
using Link.LocalDev;

namespace UnitTests.LocalDev;

/// <summary>
/// The local SQL Server, MongoDB, Redis and Azurite tags live in docker-compose.yml.
/// ServiceTests read them; they do not keep a second copy.
/// </summary>
[Trait("Category", "UnitTests")]
public class DevStackImagesTests
{
    [Fact]
    public void Parsed_images_match_the_compose_block()
    {
        var block = ReadDevImages();

        DevStackImages.SqlServer.Reference.Should().Be(block["sql"]);
        DevStackImages.Mongo.Reference.Should().Be(block["mongo"]);
        DevStackImages.Redis.Reference.Should().Be(block["redis"]);
        DevStackImages.Azurite.Reference.Should().Be(block["azurite"]);

        DevStackImages.SqlServer.Registry.Should().Be("mcr.microsoft.com");
        DevStackImages.SqlServer.Repository.Should().Be("mssql/server");
        DevStackImages.Mongo.Registry.Should().BeEmpty();
        DevStackImages.Mongo.Repository.Should().Be("mongo");
        DevStackImages.Redis.Repository.Should().Be("redis");
        DevStackImages.Azurite.Repository.Should().Be("azure-storage/azurite");
    }

    [Fact]
    public void Compose_services_use_the_shared_anchors()
    {
        var compose = File.ReadAllText(FindComposeFile());

        Count(compose, "image: *dev-sql-image").Should().Be(1);
        Count(compose, "image: *dev-mongo-image").Should().Be(2);
        Count(compose, "image: *dev-redis-image").Should().Be(1);
        Count(compose, "image: *dev-azurite-image").Should().Be(1);
    }

    [Fact]
    public void Service_tests_do_not_pin_their_own_infra_images()
    {
        var root = FindServiceTestsRoot();
        var needles = new[]
        {
            "2022-CU13",
            "mongo:7.0",
            "azurite:latest",
            "redis:latest",
            "mssql/server:",
            "mongo:6",
            "redis:6.2",
            "azurite:3"
        };

        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith($"{Path.DirectorySeparatorChar}DevStackImagesTests.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var needle in needles)
            {
                if (text.Contains(needle, StringComparison.Ordinal))
                {
                    hits.Add($"{Path.GetRelativePath(root, file)}: {needle}");
                }
            }
        }

        hits.Should().BeEmpty();
    }

    private static Dictionary<string, string> ReadDevImages()
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        var inBlock = false;
        foreach (var raw in File.ReadLines(FindComposeFile()))
        {
            var line = raw.TrimEnd();
            if (!inBlock)
            {
                if (line.Trim() == "x-dev-images:")
                {
                    inBlock = true;
                }

                continue;
            }

            if (line.Length == 0 || !char.IsWhiteSpace(line[0]))
            {
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                {
                    break;
                }

                continue;
            }

            var trimmed = line.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            var anchorEnd = value.IndexOf(' ');
            if (anchorEnd >= 0)
            {
                value = value[(anchorEnd + 1)..].Trim();
            }

            if (key is "sql" or "mongo" or "redis" or "azurite")
            {
                found[key] = value;
            }
        }

        return found;
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string FindComposeFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docker-compose.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("docker-compose.yml was not found above the test output directory.");
    }

    private static string FindServiceTestsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ServiceTests.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("ServiceTests.csproj was not found above the test output directory.");
    }
}
