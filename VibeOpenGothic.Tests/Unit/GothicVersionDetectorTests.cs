using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Ini;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class GothicVersionDetectorTests
{
    [Fact]
    public void Detect_PatchFive_ReturnsNotR()
    {
        using var temp = new TempDirectory();
        var layout = CreateLayout(temp);
        var ini = IniFile.Parse("[GAME]\nPATCHVERSION=5\n");

        var version = GothicVersionDetector.Detect(layout, ini, GameVersionOverride.Auto);

        Assert.Equal(GothicVersion.Gothic2NotR, version.Version);
        Assert.Equal(5, version.PatchVersion);
        Assert.Equal("auto", version.Source);
    }

    [Fact]
    public void Detect_PatchTwo_ReturnsClassic()
    {
        using var temp = new TempDirectory();
        var layout = CreateLayout(temp);
        var ini = IniFile.Parse("[GAME]\nPATCHVERSION=2\n");

        var version = GothicVersionDetector.Detect(layout, ini, GameVersionOverride.Auto);

        Assert.Equal(GothicVersion.Gothic2Classic, version.Version);
    }

    [Fact]
    public void Detect_AddonArchive_ReturnsNotR()
    {
        using var temp = new TempDirectory();
        var layout = CreateLayout(temp);
        temp.CreateFile(Path.Combine("Data", "Worlds_Addon.vdf"), "test");
        var ini = IniFile.Parse("[GAME]\nPATCHVERSION=2\n");

        var version = GothicVersionDetector.Detect(layout, ini, GameVersionOverride.Auto);

        Assert.Equal(GothicVersion.Gothic2NotR, version.Version);
    }

    [Fact]
    public void Detect_Gothic1OuBin_ReturnsGothic1()
    {
        using var temp = new TempDirectory();
        var layout = CreateLayout(temp);
        temp.CreateFile(Path.Combine("_work", "Data", "Scripts", "content", "CUTSCENE", "OU.BIN"), "test");
        var ini = IniFile.Parse("[GAME]\n");

        var version = GothicVersionDetector.Detect(layout, ini, GameVersionOverride.Auto);

        Assert.Equal(GothicVersion.Gothic1, version.Version);
    }

    [Theory]
    [InlineData(GameVersionOverride.Gothic1, GothicVersion.Gothic1)]
    [InlineData(GameVersionOverride.Gothic2Classic, GothicVersion.Gothic2Classic)]
    [InlineData(GameVersionOverride.Gothic2NotR, GothicVersion.Gothic2NotR)]
    public void Detect_Override_Wins(GameVersionOverride requested, GothicVersion expected)
    {
        using var temp = new TempDirectory();
        var layout = CreateLayout(temp);
        var ini = IniFile.Parse("[GAME]\nPATCHVERSION=5\n");

        var version = GothicVersionDetector.Detect(layout, ini, requested);

        Assert.Equal(expected, version.Version);
        Assert.NotEqual("auto", version.Source);
    }

    private static GothicInstallLayout CreateLayout(TempDirectory temp)
    {
        var system = temp.CreateDirectory("system");
        var data = temp.CreateDirectory("Data");
        var work = temp.CreateDirectory("_work");
        temp.CreateDirectory(Path.Combine("_work", "Data"));
        return new GothicInstallLayout(temp.Root, system, data, work);
    }
}
