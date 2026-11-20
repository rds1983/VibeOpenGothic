namespace VibeOpenGothic.Data;

/// <summary>
/// A Gothic installation root. This type only describes the layout; validation
/// and full VFS mounting arrive with T0.3.
/// </summary>
public sealed class GothicInstall
{
    public GothicInstall(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
    }

    public string RootPath { get; }

    public string SystemDirectory => Path.Combine(RootPath, "System");

    public string DataDirectory => Path.Combine(RootPath, "Data");

    public string GothicIniPath => Path.Combine(SystemDirectory, "Gothic.ini");

    public bool HasSystemIni => File.Exists(GothicIniPath);

    public bool HasDataDirectory => Directory.Exists(DataDirectory);

    public IReadOnlyList<string> EnumerateTopLevelVdfFiles()
    {
        if (!HasDataDirectory)
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(DataDirectory, "*.vdf", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public override string ToString()
    {
        return RootPath;
    }
}
