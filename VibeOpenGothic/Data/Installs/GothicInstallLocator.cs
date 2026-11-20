using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Data.Ini;

namespace VibeOpenGothic.Data.Installs;

/// <summary>
/// Resolves a Gothic installation. Order: command line, VOG_GOTHIC_PATH,
/// settings, known locations. A command-line path is authoritative: if it is
/// invalid, the lookup fails instead of silently using another install.
/// </summary>
public static class GothicInstallLocator
{
    public static GothicInstallLookupResult Locate(
        string? commandLinePath,
        string? environmentPath,
        string? settingsPath,
        GameVersionOverride versionOverride,
        string? modManifestPath,
        IEnumerable<string>? knownCandidates = null)
    {
        var searched = new List<string>();
        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(commandLinePath))
        {
            var attempt = TryLoad(commandLinePath, versionOverride, modManifestPath, searched);
            return attempt.Install is not null
                ? Success(attempt, searched)
                : new GothicInstallLookupResult(null, attempt.Errors, warnings, searched);
        }

        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            var attempt = TryLoad(environmentPath, versionOverride, modManifestPath, searched);
            if (attempt.Install is not null)
            {
                return Success(attempt, searched, warnings);
            }

            warnings.AddRange(attempt.Errors.Select(error => $"VOG_GOTHIC_PATH: {error}"));
        }

        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            var attempt = TryLoad(settingsPath, versionOverride, modManifestPath, searched);
            if (attempt.Install is not null)
            {
                return Success(attempt, searched, warnings);
            }

            warnings.AddRange(attempt.Errors.Select(error => $"settings.gothicPath: {error}"));
        }

        foreach (var candidate in knownCandidates ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(candidate) ||
                searched.Contains(candidate, PathResolver.PathComparer))
            {
                continue;
            }

            var attempt = TryLoad(candidate, versionOverride, modManifestPath, searched);
            if (attempt.Install is not null)
            {
                return Success(attempt, searched, warnings);
            }
        }

        var errors = new List<string>
        {
            "No valid Gothic installation found. Pass -g <path> pointing to the Gothic II root directory.",
        };

        return new GothicInstallLookupResult(null, errors, warnings, searched);
    }

    private static GothicInstallLookupResult Success(
        LoadAttempt attempt,
        List<string> searched,
        List<string>? collectedWarnings = null)
    {
        var warnings = new List<string>();
        if (collectedWarnings is not null)
        {
            warnings.AddRange(collectedWarnings);
        }

        if (attempt.Install is not null)
        {
            warnings.AddRange(attempt.Install.Warnings);
        }

        return new GothicInstallLookupResult(
            attempt.Install,
            Array.Empty<string>(),
            warnings,
            searched);
    }

    private static LoadAttempt TryLoad(
        string requestedPath,
        GameVersionOverride versionOverride,
        string? modManifestPath,
        List<string> searched)
    {
        var attempt = new LoadAttempt();

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(requestedPath.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            attempt.Errors.Add($"Invalid Gothic installation path '{requestedPath}': {exception.Message}");
            return attempt;
        }

        if (!searched.Contains(fullPath, PathResolver.PathComparer))
        {
            searched.Add(fullPath);
        }

        if (!Directory.Exists(fullPath))
        {
            attempt.Errors.Add($"Gothic installation not found at '{fullPath}'.");
            return attempt;
        }

        var systemDirectory = PathResolver.ResolveExisting(fullPath, "System");
        var gothicIniPath = systemDirectory is null
            ? null
            : PathResolver.FindChild(systemDirectory, "Gothic.ini");
        var dataDirectory = PathResolver.ResolveExisting(fullPath, "Data");
        var workDirectory = PathResolver.ResolveExisting(fullPath, "_work");
        var workDataDirectory = workDirectory is null
            ? null
            : PathResolver.ResolveExisting(workDirectory, "Data");
        var scriptsDirectory = workDirectory is null
            ? null
            : PathResolver.ResolveExisting(workDirectory, "Data/Scripts/_compiled");

        if (systemDirectory is null || gothicIniPath is null)
        {
            attempt.Errors.Add($"'{fullPath}' is not a Gothic installation: System/Gothic.ini is missing.");
        }

        if (dataDirectory is null)
        {
            attempt.Errors.Add($"'{fullPath}' is not a Gothic installation: the Data directory is missing.");
        }

        if (workDirectory is null || workDataDirectory is null)
        {
            attempt.Errors.Add($"'{fullPath}' is not a Gothic installation: _work/Data is missing.");
        }

        if (scriptsDirectory is null)
        {
            attempt.Errors.Add(
                $"'{fullPath}' is not a Gothic installation: _work/Data/Scripts/_compiled is missing.");
        }

        if (attempt.Errors.Count > 0)
        {
            return attempt;
        }

        IniFile gothicIni;
        try
        {
            gothicIni = IniFile.Load(gothicIniPath!);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            attempt.Errors.Add($"Unable to read '{gothicIniPath}': {exception.Message}");
            return attempt;
        }

        var layout = new GothicInstallLayout(
            fullPath,
            systemDirectory!,
            dataDirectory!,
            workDirectory!);

        var version = GothicVersionDetector.Detect(layout, gothicIni, versionOverride);

        var mod = ModManifest.Load(modManifestPath, systemDirectory!, out var modError);
        if (modError is not null)
        {
            attempt.Errors.Add(modError);
            return attempt;
        }

        var archives = ArchiveScanner.Scan(dataDirectory!, mod, out var archiveWarnings);
        if (archives.Count == 0)
        {
            attempt.Errors.Add($"No VDF archives found in '{dataDirectory}'.");
            return attempt;
        }

        var warnings = new List<string>(archiveWarnings);

        attempt.Install = new GothicInstall(
            layout,
            gothicIniPath!,
            scriptsDirectory!,
            version,
            mod,
            archives,
            warnings);

        return attempt;
    }

    private sealed class LoadAttempt
    {
        public GothicInstall? Install { get; set; }

        public List<string> Errors { get; } = new();
    }
}
