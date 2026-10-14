using System.IO.Compression;
using System.Text;
using Automation.UI.Services;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ZipSectionCommitTests
{
    [Fact]
    public async Task CommitAsync_ReplacesTheZipOnlyAfterTheMutationSucceeds()
    {
        var path = CreateZip(("keep.txt", "keep"), ("notes.txt", "notes"));

        try
        {
            await ZipSectionCommit.CommitAsync(path, (archive, _) =>
            {
                WriteEntry(archive, "abs/a.txt", "patient");
                return Task.CompletedTask;
            }, CancellationToken.None);

            Assert.False(File.Exists(path + ".pending"));
            Assert.Equal("keep", ReadEntry(path, "keep.txt"));
            Assert.Equal("notes", ReadEntry(path, "notes.txt"));
            Assert.Equal("patient", ReadEntry(path, "abs/a.txt"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CommitAsync_LeavesTheOriginalZipWhenTheMutationFails()
    {
        var path = CreateZip(("keep.txt", "keep"));

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ZipSectionCommit.CommitAsync(path, (archive, _) =>
                {
                    WriteEntry(archive, "abs/a.txt", "partial");
                    throw new InvalidOperationException("copy failed");
                }, CancellationToken.None));

            Assert.False(File.Exists(path + ".pending"));
            Assert.Equal("keep", ReadEntry(path, "keep.txt"));
            Assert.Equal(["keep.txt"], EntryNames(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateZip(params (string Name, string Text)[] entries)
    {
        var path = Path.Combine(Path.GetTempPath(), "link-zip-commit-" + Guid.NewGuid().ToString("N") + ".zip");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (name, text) in entries)
            WriteEntry(archive, name, text);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static List<string> EntryNames(string path)
    {
        using var file = File.OpenRead(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        return archive.Entries.Select(entry => entry.FullName).ToList();
    }

    private static string ReadEntry(string path, string name)
    {
        using var file = File.OpenRead(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var entry = archive.GetEntry(name);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
