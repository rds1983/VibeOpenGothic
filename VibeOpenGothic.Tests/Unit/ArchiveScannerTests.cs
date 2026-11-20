using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class ArchiveScannerTests
{
    [Fact]
    public void Scan_CollectsVdfsAndSkipsEmptyAndNxDirectories()
    {
        using var temp = new TempDirectory();
        var data = temp.CreateDirectory("Data");

        VdfArchiveTests.WriteVdf(Path.Combine(data, "A.vdf"), 10);
        VdfArchiveTests.WriteVdf(Path.Combine(data, "B_Addon.vdf"), 200);
        VdfArchiveTests.WriteVdf(Path.Combine(data, "mod1.mod"), 50);
        VdfArchiveTests.WriteVdf(Path.Combine(data, "mod2.mod"), 300);
        temp.CreateFile(Path.Combine("Data", "empty.vdf"), string.Empty);

        var nxDirectory = Path.Combine(data, "Textures.NX.EN");
        Directory.CreateDirectory(nxDirectory);
        VdfArchiveTests.WriteVdf(Path.Combine(nxDirectory, "skip.vdf"), 999);

        var archives = ArchiveScanner.Scan(data, ModManifest.Empty, out var warnings);

        Assert.Empty(warnings);
        Assert.Equal(
            new[] { "B_Addon.vdf", "A.vdf" },
            archives.Select(x => x.Name));
        Assert.All(archives, archive => Assert.True(archive.SizeBytes > 0));
    }

    [Fact]
    public void Scan_WithManifestWithoutVdfFilter_IncludesAllMods()
    {
        using var temp = new TempDirectory();
        var data = temp.CreateDirectory("Data");
        var system = temp.CreateDirectory("system");
        VdfArchiveTests.WriteVdf(Path.Combine(data, "mod1.mod"), 10);
        VdfArchiveTests.WriteVdf(Path.Combine(data, "mod2.mod"), 20);
        temp.CreateFile(Path.Combine("system", "mod.ini"), "[FILES]\nGAME=MODGAME\n");

        var mod = ModManifest.Load("mod.ini", system, out var error);

        Assert.Null(error);
        var archives = ArchiveScanner.Scan(data, mod, out _);

        Assert.Equal(new[] { "mod2.mod", "mod1.mod" }, archives.Select(x => x.Name));
    }

    [Fact]
    public void Scan_WithVdfFilter_OnlyIncludesListedMods()
    {
        using var temp = new TempDirectory();
        var data = temp.CreateDirectory("Data");
        var system = temp.CreateDirectory("system");
        VdfArchiveTests.WriteVdf(Path.Combine(data, "mod1.mod"), 10);
        VdfArchiveTests.WriteVdf(Path.Combine(data, "mod2.mod"), 20);
        temp.CreateFile(
            Path.Combine("system", "mod.ini"),
            "[FILES]\nVDF=mod1.mod\n");

        var mod = ModManifest.Load("mod.ini", system, out var error);

        Assert.Null(error);
        var archives = ArchiveScanner.Scan(data, mod, out _);

        Assert.Single(archives);
        Assert.Equal("mod1.mod", archives[0].Name);
    }

    [Fact]
    public void Scan_MissingDataDirectory_Warns()
    {
        using var temp = new TempDirectory();

        var archives = ArchiveScanner.Scan(
            Path.Combine(temp.Root, "missing"),
            ModManifest.Empty,
            out var warnings);

        Assert.Empty(archives);
        Assert.Single(warnings);
    }
}
