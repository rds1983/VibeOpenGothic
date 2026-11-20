using System.Text;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Probe;
using VibeOpenGothic.Data.Vfs;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class InstallProbeTests
{
    [Fact]
    public void Create_FindsCoreAssets()
    {
        using var temp = new TempDirectory();
        using var vfs = new InMemoryGameVfs();
        AddFile(vfs, "/_WORK/DATA/Scripts/_compiled/GOTHIC.DAT");
        AddFile(vfs, "/_WORK/DATA/WORLDS/NEWWORLD/NEWWORLD.ZEN");
        AddFile(vfs, "/_WORK/DATA/TEXTURES/_COMPILED/A.TEX");
        AddFile(vfs, "/_WORK/DATA/ANIMS/HUMANS.MDS");

        var install = CreateInstall(temp, GothicVersion.Gothic2NotR);
        var result = InstallProbe.Create(install, vfs);

        Assert.True(result.Success);
        var text = string.Join("\n", result.Lines);
        Assert.Contains("GOTHIC.DAT", text);
        Assert.Contains("newworld.zen", text);
        Assert.Contains("A.TEX", text);
        Assert.Contains("HUMANS.MDS", text);
        Assert.Contains("version:", text);
    }

    [Fact]
    public void Create_MissingWorld_ReportsFailure()
    {
        using var temp = new TempDirectory();
        using var vfs = new InMemoryGameVfs();
        AddFile(vfs, "/_WORK/DATA/Scripts/_compiled/GOTHIC.DAT");

        var install = CreateInstall(temp, GothicVersion.Gothic2NotR);
        var result = InstallProbe.Create(install, vfs);

        Assert.False(result.Success);
        Assert.Contains("(missing)", string.Join("\n", result.Lines));
    }

    [Fact]
    public void Create_Gothic1_UsesWorldZenDefault()
    {
        using var temp = new TempDirectory();
        using var vfs = new InMemoryGameVfs();
        AddFile(vfs, "/_WORK/DATA/Scripts/_compiled/GOTHIC.DAT");
        AddFile(vfs, "/_WORK/DATA/WORLDS/WORLD.ZEN");

        var install = CreateInstall(temp, GothicVersion.Gothic1);
        var result = InstallProbe.Create(install, vfs);

        Assert.True(result.Success);
        Assert.Contains("world.zen", string.Join("\n", result.Lines));
    }

    private static void AddFile(InMemoryGameVfs vfs, string path)
    {
        vfs.Add(path, Encoding.UTF8.GetBytes("test"));
    }

    private static GothicInstall CreateInstall(TempDirectory temp, GothicVersion version)
    {
        var layout = new GothicInstallLayout(
            temp.Root,
            Path.Combine(temp.Root, "system"),
            Path.Combine(temp.Root, "Data"),
            Path.Combine(temp.Root, "_work"));
        return new GothicInstall(
            layout,
            Path.Combine(temp.Root, "system", "Gothic.ini"),
            Path.Combine(temp.Root, "_work", "Data", "Scripts", "_compiled"),
            new GothicVersionInfo(version, version == GothicVersion.Gothic2NotR ? 5 : 0, "test"),
            ModManifest.Empty,
            Array.Empty<VdfArchive>(),
            Array.Empty<string>());
    }
}
