namespace VibeOpenGothic.Data.Installs;

public static class ArchiveScanner
{
    public static IReadOnlyList<VdfArchive> Scan(
        string dataDirectory,
        ModManifest mod,
        out IReadOnlyList<string> warnings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(mod);

        var archives = new List<VdfArchive>();
        var warningList = new List<string>();

        if (!Directory.Exists(dataDirectory))
        {
            warningList.Add($"Data directory not found: '{dataDirectory}'.");
            warnings = warningList;
            return Array.Empty<VdfArchive>();
        }

        var order = 0;

        try
        {
            Visit(dataDirectory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            warningList.Add($"Unable to enumerate archives in '{dataDirectory}': {exception.Message}");
        }

        warnings = warningList;
        return MountPlanner.Order(archives);

        void Visit(string directory)
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                if (Path.GetFileName(subdirectory).Contains(".NX.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Visit(subdirectory);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var extension = Path.GetExtension(file);
                var isVdf = extension.Equals(".vdf", StringComparison.OrdinalIgnoreCase);
                var isMod = extension.Equals(".mod", StringComparison.OrdinalIgnoreCase);

                if (!isVdf && !isMod)
                {
                    continue;
                }

                var info = new FileInfo(file);
                if (info.Length == 0)
                {
                    continue;
                }

                if (isMod && !mod.IncludesModArchive(info.Name))
                {
                    continue;
                }

                archives.Add(new VdfArchive(
                    info.FullName,
                    info.Name,
                    VdfTimestampReader.Read(info.FullName),
                    isMod,
                    order++,
                    info.Length));
            }
        }
    }
}
