namespace VibeOpenGothic.Data;

/// <summary>
/// Minimal installation lookup for the application shell. T0.3 adds automatic
/// discovery from the environment, settings and known install locations, plus
/// VDF mounting.
/// </summary>
public static class GothicInstallLocator
{
    public static GothicInstallLookupResult Locate(string? commandLinePath, string? settingsPath)
    {
        var errors = new List<string>();
        var searchedPaths = new List<string>();

        var candidate = !string.IsNullOrWhiteSpace(commandLinePath)
            ? commandLinePath
            : settingsPath;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            errors.Add(
                "No Gothic installation configured. Pass -g <path> pointing to the Gothic II root directory.");
            return new GothicInstallLookupResult(null, errors, searchedPaths);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add($"Invalid Gothic installation path '{candidate}': {exception.Message}");
            return new GothicInstallLookupResult(null, errors, searchedPaths);
        }

        searchedPaths.Add(fullPath);

        if (!Directory.Exists(fullPath))
        {
            errors.Add($"Gothic installation not found at '{fullPath}'.");
            return new GothicInstallLookupResult(null, errors, searchedPaths);
        }

        var install = new GothicInstall(fullPath);

        if (!install.HasSystemIni)
        {
            errors.Add($"'{fullPath}' is not a Gothic installation: System/Gothic.ini is missing.");
        }

        if (!install.HasDataDirectory)
        {
            errors.Add($"'{fullPath}' is not a Gothic installation: the Data directory is missing.");
        }

        return errors.Count == 0
            ? new GothicInstallLookupResult(install, errors, searchedPaths)
            : new GothicInstallLookupResult(null, errors, searchedPaths);
    }
}
