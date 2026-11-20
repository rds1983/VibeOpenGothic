using VibeOpenGothic.Data;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class GothicInstallLocatorTests
{
    [Fact]
    public void Locate_ValidInstallation_Succeeds()
    {
        using var temp = new TempDirectory();
        CreateInstall(temp);
        temp.CreateFile(Path.Combine("Data", "GOTHIC.VDF"), string.Empty);

        var result = GothicInstallLocator.Locate(temp.Root, null);

        Assert.True(result.Success);
        var install = result.Install;
        Assert.NotNull(install);
        Assert.Equal(Path.GetFullPath(temp.Root), install.RootPath);
        Assert.True(install.HasSystemIni);
        Assert.True(install.HasDataDirectory);
        Assert.Single(install.EnumerateTopLevelVdfFiles());
    }

    [Fact]
    public void Locate_CommandLinePathWinsOverSettingsPath()
    {
        using var commandLine = new TempDirectory();
        using var settings = new TempDirectory();
        CreateInstall(commandLine);
        CreateInstall(settings);

        var result = GothicInstallLocator.Locate(commandLine.Root, settings.Root);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(commandLine.Root), result.Install?.RootPath);
    }

    [Fact]
    public void Locate_FallsBackToSettingsPath()
    {
        using var temp = new TempDirectory();
        CreateInstall(temp);

        var result = GothicInstallLocator.Locate(null, temp.Root);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(temp.Root), result.Install?.RootPath);
    }

    [Fact]
    public void Locate_NoConfiguredPath_ReportsError()
    {
        var result = GothicInstallLocator.Locate(null, null);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("-g", result.Errors[0]);
    }

    [Fact]
    public void Locate_MissingDirectory_ReportsError()
    {
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.Root, "missing");

        var result = GothicInstallLocator.Locate(missing, null);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("not found", result.Errors[0]);
    }

    [Fact]
    public void Locate_MissingGothicIni_ReportsError()
    {
        using var temp = new TempDirectory();
        temp.CreateDirectory("Data");

        var result = GothicInstallLocator.Locate(temp.Root, null);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("Gothic.ini", result.Errors[0]);
    }

    [Fact]
    public void Locate_MissingDataDirectory_ReportsError()
    {
        using var temp = new TempDirectory();
        temp.CreateFile(Path.Combine("System", "Gothic.ini"), "[GAME]\n");

        var result = GothicInstallLocator.Locate(temp.Root, null);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("Data", result.Errors[0]);
    }

    private static void CreateInstall(TempDirectory temp)
    {
        temp.CreateFile(Path.Combine("System", "Gothic.ini"), "[GAME]\n");
        temp.CreateDirectory("Data");
    }
}
