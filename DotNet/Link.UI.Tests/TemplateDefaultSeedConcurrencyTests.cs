using System.IO;
using Xunit;

namespace Link.UI.Tests;

public class TemplateDefaultSeedConcurrencyTests
{
    [Fact]
    public void Setting_the_default_template_retries_write_conflicts_from_parallel_starts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var source = File.ReadAllText(Path.Combine(dir!.FullName, "DotNet", "Link.UI", "Engine", "Services", "Persistence", "MongoOrganizationResourceMapTemplateStore.cs"));
        var setDefault = source.Substring(source.IndexOf("SetDefaultAsync(Guid id", StringComparison.Ordinal));
        setDefault = setDefault.Substring(0, setDefault.IndexOf("DeleteAsync", StringComparison.Ordinal));

        Assert.Contains("WithTransactionAsync", setDefault);
        Assert.DoesNotContain("StartTransaction()", setDefault);
    }
}
