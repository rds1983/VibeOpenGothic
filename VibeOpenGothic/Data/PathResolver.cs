namespace VibeOpenGothic.Data;

/// <summary>
/// Case-insensitive path resolution against the host file system. Gothic
/// installations are not consistent about directory casing (`system` vs
/// `System`), and Linux file systems are case-sensitive.
/// </summary>
public static class PathResolver
{
    public static string? ResolveExisting(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(relativePath);

        if (!Directory.Exists(root))
        {
            return null;
        }

        var current = Path.GetFullPath(root);

        foreach (var part in Split(relativePath))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }

            var child = FindChild(current, part);
            if (child is null)
            {
                return null;
            }

            current = child;
        }

        return current;
    }

    public static string? FindChild(string directory, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Directory.Exists(directory))
        {
            return null;
        }

        string? caseInsensitiveMatch = null;

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var entryName = Path.GetFileName(entry);

            if (string.Equals(entryName, name, StringComparison.Ordinal))
            {
                return entry;
            }

            if (caseInsensitiveMatch is null &&
                string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
            {
                caseInsensitiveMatch = entry;
            }
        }

        return caseInsensitiveMatch;
    }

    public static IEnumerable<string> Split(string path)
    {
        return path.Split(
            new[] { '\\', '/' },
            StringSplitOptions.RemoveEmptyEntries);
    }

    public static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
