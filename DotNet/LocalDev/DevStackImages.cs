namespace Link.LocalDev;

/// <summary>
/// SQL Server, MongoDB, Redis and Azurite images for local development.
/// The tags are the <c>x-dev-images</c> block in <c>docker-compose.yml</c>.
/// ServiceTests and the Aspire AppHost both read that block, so a tag lives in one place.
/// </summary>
public static class DevStackImages
{
    private static readonly Lazy<DevStackImageSet> Images = new(Load);

    public static DevStackImage SqlServer => Images.Value.SqlServer;

    public static DevStackImage Mongo => Images.Value.Mongo;

    public static DevStackImage Redis => Images.Value.Redis;

    public static DevStackImage Azurite => Images.Value.Azurite;

    private static DevStackImageSet Load()
    {
        var composeFile = FindComposeFile();
        var found = new Dictionary<string, DevStackImage>(StringComparer.Ordinal);
        var inBlock = false;

        foreach (var rawLine in File.ReadLines(composeFile))
        {
            var line = rawLine.TrimEnd();
            if (!inBlock)
            {
                if (line.Trim() == "x-dev-images:")
                {
                    inBlock = true;
                }

                continue;
            }

            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            // The next top-level key ends the block. Entries are indented.
            if (!char.IsWhiteSpace(line[0]))
            {
                break;
            }

            var trimmed = line.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            if (value.StartsWith('&'))
            {
                var anchorEnd = value.IndexOf(' ');
                if (anchorEnd < 0)
                {
                    throw new InvalidOperationException(
                        $"x-dev-images.{key} in {composeFile} has an anchor and no image.");
                }

                value = value[(anchorEnd + 1)..].Trim();
            }

            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            if (key is "sql" or "mongo" or "redis" or "azurite")
            {
                found[key] = DevStackImage.Parse(value, $"{composeFile} x-dev-images.{key}");
            }
        }

        if (!inBlock)
        {
            throw new InvalidOperationException($"docker-compose.yml has no x-dev-images block: {composeFile}");
        }

        return new DevStackImageSet(
            Require(found, "sql", composeFile),
            Require(found, "mongo", composeFile),
            Require(found, "redis", composeFile),
            Require(found, "azurite", composeFile));
    }

    private static DevStackImage Require(Dictionary<string, DevStackImage> found, string key, string composeFile)
    {
        if (!found.TryGetValue(key, out var image))
        {
            throw new InvalidOperationException($"x-dev-images.{key} is missing from {composeFile}.");
        }

        return image;
    }

    private static string FindComposeFile()
    {
        var starts = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "docker-compose.yml");
                if (File.Exists(candidate))
                {
                    foreach (var line in File.ReadLines(candidate))
                    {
                        if (line.Trim() == "x-dev-images:")
                        {
                            return candidate;
                        }
                    }
                }

                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not find a docker-compose.yml with an x-dev-images block. Run from the link-cloud repository.");
    }

    private sealed record DevStackImageSet(DevStackImage SqlServer, DevStackImage Mongo, DevStackImage Redis, DevStackImage Azurite);
}

/// <summary>One image reference from <c>x-dev-images</c>.</summary>
public readonly record struct DevStackImage(string Registry, string Repository, string Tag)
{
    /// <summary><c>repository:tag</c>, or <c>registry/repository:tag</c> when a registry is set.</summary>
    public string Reference => string.IsNullOrEmpty(Registry)
        ? $"{Repository}:{Tag}"
        : $"{Registry}/{Repository}:{Tag}";

    /// <summary>
    /// The image name <c>AddContainer</c> takes. A registry stays in the name, matching the compose
    /// services that already pass <c>mcr.microsoft.com/...</c> as the image.
    /// </summary>
    public string ContainerImage => string.IsNullOrEmpty(Registry) ? Repository : $"{Registry}/{Repository}";

    public static DevStackImage Parse(string reference, string source)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Contains(' ') || reference.Contains('@'))
        {
            throw new InvalidOperationException($"Image reference '{reference}' from {source} is not a name:tag.");
        }

        var colon = reference.LastIndexOf(':');
        if (colon <= 0 || colon == reference.Length - 1 || reference[(colon + 1)..].Contains('/'))
        {
            throw new InvalidOperationException($"Image reference '{reference}' from {source} has no tag.");
        }

        var name = reference[..colon];
        var tag = reference[(colon + 1)..];
        var slash = name.IndexOf('/');
        var registry = "";
        var repository = name;
        if (slash > 0)
        {
            var first = name[..slash];
            if (first.Contains('.') || first.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                registry = first;
                repository = name[(slash + 1)..];
            }
        }

        if (repository.Length == 0 || tag.Length == 0)
        {
            throw new InvalidOperationException($"Image reference '{reference}' from {source} is not a name:tag.");
        }

        return new DevStackImage(registry, repository, tag);
    }
}
