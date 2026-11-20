namespace VibeOpenGothic.Core.Settings;

/// <summary>
/// Resolves the writable application directory. Gothic installations are
/// read-only; settings, logs and saves never go there.
/// </summary>
public sealed record AppPaths(string DataDirectory)
{
    public string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    public string LogFilePath => Path.Combine(DataDirectory, "log.txt");

    public string SavesDirectory => Path.Combine(DataDirectory, "saves");

    public static AppPaths Create(string? overrideDirectory)
    {
        string dataDirectory;

        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            dataDirectory = Path.GetFullPath(overrideDirectory);
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = AppContext.BaseDirectory;
            }

            dataDirectory = Path.Combine(localAppData, "VibeOpenGothic");
        }

        return new AppPaths(dataDirectory);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(SavesDirectory);
    }
}
