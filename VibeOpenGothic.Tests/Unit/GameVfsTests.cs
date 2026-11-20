using System.Text;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class GameVfsTests
{
    [Fact]
    public void InMemoryVfs_ResolvesPathsAndNamesCaseInsensitively()
    {
        using var vfs = new InMemoryGameVfs();
        vfs.Add("/_WORK/DATA/Textures/A.TEX", new byte[] { 1, 2, 3 });

        Assert.True(vfs.FileExists("/_work/data/textures/a.tex"));
        Assert.True(vfs.FileExists("a.tex"));
        Assert.True(vfs.DirectoryExists("/_WORK/DATA"));
        Assert.False(vfs.DirectoryExists("/_WORK/MISSING"));

        var entry = vfs.Find("a.tex");
        Assert.NotNull(entry);
        Assert.Equal("/_WORK/DATA/Textures/A.TEX", entry.Path);

        using var stream = vfs.OpenRead("/_work/data/textures/a.tex");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        Assert.Equal(new byte[] { 1, 2, 3 }, memory.ToArray());

        Assert.Single(vfs.EnumerateFiles());
    }

    [Fact]
    public void ZenKitVfs_MountsLooseWorkFiles()
    {
        using var temp = new TempDirectory();
        var work = temp.CreateDirectory("_work");
        temp.CreateFile(Path.Combine("_work", "Data", "foo.txt"), "hello");

        var layout = new GothicInstallLayout(
            temp.Root,
            Path.Combine(temp.Root, "system"),
            Path.Combine(temp.Root, "Data"),
            work);
        var install = new GothicInstall(
            layout,
            Path.Combine(temp.Root, "system", "Gothic.ini"),
            Path.Combine(work, "Data", "Scripts", "_compiled"),
            new GothicVersionInfo(GothicVersion.Gothic2NotR, 5, "test"),
            ModManifest.Empty,
            Array.Empty<VdfArchive>(),
            Array.Empty<string>());

        using var vfs = ZenKitGameVfs.Mount(install, out var warnings);

        Assert.Empty(warnings);
        Assert.True(vfs.FileExists("foo.txt"));
        Assert.True(vfs.FileExists("/_work/Data/foo.txt"));
        Assert.True(vfs.DirectoryExists("/_work/Data"));

        using var reader = new StreamReader(vfs.OpenRead("foo.txt"), Encoding.UTF8);
        Assert.Equal("hello", reader.ReadToEnd());

        Assert.Contains(vfs.EnumerateFiles(), file => file.Path == "/_work/Data/foo.txt");
    }
}
