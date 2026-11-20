using VibeOpenGothic.Core.Settings;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaultsWithoutWarnings()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "settings.json");

        var result = SettingsStore.Load(path);

        Assert.Empty(result.Warnings);
        Assert.Equal(AppSettings.DefaultWindowWidth, result.Settings.WindowWidth);
        Assert.Equal(AppSettings.DefaultWindowHeight, result.Settings.WindowHeight);
        Assert.True(result.Settings.Windowed);
        Assert.True(result.Settings.VSync);
        Assert.Null(result.Settings.GothicPath);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllValues()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "nested", "settings.json");
        var settings = new AppSettings
        {
            GothicPath = @"C:\Gothic II",
            WindowWidth = 1920,
            WindowHeight = 1080,
            Windowed = false,
            VSync = false,
        };

        SettingsStore.Save(path, settings);
        var result = SettingsStore.Load(path);

        Assert.Empty(result.Warnings);
        Assert.Equal(settings.GothicPath, result.Settings.GothicPath);
        Assert.Equal(1920, result.Settings.WindowWidth);
        Assert.Equal(1080, result.Settings.WindowHeight);
        Assert.False(result.Settings.Windowed);
        Assert.False(result.Settings.VSync);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Load_MalformedJson_ReturnsDefaultsWithWarning()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("settings.json", "{ not valid json");

        var result = SettingsStore.Load(path);

        Assert.Single(result.Warnings);
        Assert.Equal(AppSettings.DefaultWindowWidth, result.Settings.WindowWidth);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaultsWithWarning()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("settings.json", "");

        var result = SettingsStore.Load(path);

        Assert.Single(result.Warnings);
        Assert.Equal(AppSettings.DefaultWindowWidth, result.Settings.WindowWidth);
    }

    [Fact]
    public void Load_InvalidWindowSize_ClampsAndWarns()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile(
            "settings.json",
            """{ "windowWidth": 10, "windowHeight": 9000 }""");

        var result = SettingsStore.Load(path);

        Assert.Equal(2, result.Warnings.Count);
        Assert.Equal(AppSettings.DefaultWindowWidth, result.Settings.WindowWidth);
        Assert.Equal(AppSettings.DefaultWindowHeight, result.Settings.WindowHeight);
    }

    [Fact]
    public void Load_UnknownProperties_AreIgnored()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile(
            "settings.json",
            """{ "windowWidth": 1600, "windowHeight": 900, "futureSetting": true }""");

        var result = SettingsStore.Load(path);

        Assert.Empty(result.Warnings);
        Assert.Equal(1600, result.Settings.WindowWidth);
        Assert.Equal(900, result.Settings.WindowHeight);
    }

    [Fact]
    public void Normalize_ValidValues_ReturnsNoWarnings()
    {
        var settings = new AppSettings
        {
            WindowWidth = 1280,
            WindowHeight = 720,
        };

        var warnings = settings.Normalize();

        Assert.Empty(warnings);
    }
}
