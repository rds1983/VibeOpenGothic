using Microsoft.Win32;

namespace VibeOpenGothic.Data.Installs;

/// <summary>
/// Common Gothic II install locations. Explicit CLI, environment and settings
/// paths take precedence; this list is the last fallback.
/// </summary>
public static class KnownInstallLocations
{
    public static IReadOnlyList<string> Enumerate()
    {
        var candidates = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            AddIfExists(
                candidates,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "JoWooD",
                    "Gothic II"));
            AddIfExists(
                candidates,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "JoWooD",
                    "Gothic II"));
            AddIfExists(candidates, @"C:\Gothic II");

            var steamPath = ReadSteamPath();
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                AddSteamCandidates(candidates, steamPath);
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddSteamCandidates(candidates, Path.Combine(home, ".steam", "steam"));
            AddSteamCandidates(candidates, Path.Combine(home, ".local", "share", "Steam"));
            AddSteamCandidates(
                candidates,
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
        }

        return candidates
            .Distinct(PathResolver.PathComparer)
            .ToArray();
    }

    private static void AddSteamCandidates(List<string> candidates, string steamRoot)
    {
        AddIfExists(candidates, Path.Combine(steamRoot, "steamapps", "common", "Gothic II"));

        IReadOnlyList<string> libraries;
        try
        {
            libraries = SteamLibraryParser.ReadLibraryFolders(steamRoot);
        }
        catch
        {
            return;
        }

        foreach (var library in libraries)
        {
            AddIfExists(candidates, Path.Combine(library, "steamapps", "common", "Gothic II"));
        }
    }

    private static void AddIfExists(List<string> candidates, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            candidates.Add(Path.GetFullPath(path));
        }
    }

    private static string? ReadSteamPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return Registry.GetValue(
                       @"HKEY_CURRENT_USER\Software\Valve\Steam",
                       "SteamPath",
                       null) as string
                   ?? Registry.GetValue(
                       @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                       "InstallPath",
                       null) as string
                   ?? Registry.GetValue(
                       @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
                       "InstallPath",
                       null) as string;
        }
        catch
        {
            return null;
        }
    }
}
