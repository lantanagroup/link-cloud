using System.Diagnostics;
using System.Reflection;

namespace Automation.UI.Services;

public static class ThetisRevision
{
    public static string? TryGetGitSha()
    {
        try
        {
            var root = FindThetisRoot();
            if (root == null)
                return null;

            var start = new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(start);
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(3000) || process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            return output;
        }
        catch
        {
            return null;
        }
    }

    public static string? TryGetAssemblyInformationalVersion()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "Thetis.Generation.Engine", StringComparison.OrdinalIgnoreCase))
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.Contains("Thetis", StringComparison.OrdinalIgnoreCase) == true);

        return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }

    private static string? FindThetisRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var sibling = Path.Combine(dir.FullName, "thetis");
            if (LooksLikeThetis(sibling))
                return sibling;

            if (dir.Parent != null)
            {
                sibling = Path.Combine(dir.Parent.FullName, "thetis");
                if (LooksLikeThetis(sibling))
                    return sibling;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool LooksLikeThetis(string path) =>
        Directory.Exists(Path.Combine(path, ".git"))
        || File.Exists(Path.Combine(path, "Thetis.Generation.Engine", "Thetis.Generation.Engine.csproj"));
}
