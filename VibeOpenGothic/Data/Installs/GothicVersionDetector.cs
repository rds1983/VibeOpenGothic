using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Data.Ini;

namespace VibeOpenGothic.Data.Installs;

public static class GothicVersionDetector
{
    public static GothicVersionInfo Detect(
        GothicInstallLayout layout,
        IniFile gothicIni,
        GameVersionOverride versionOverride)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(gothicIni);

        var gothic1Score = 0;
        var gothic2Score = 0;

        var normalizedRoot = layout.RootPath.Replace('\\', '/');
        if (normalizedRoot.Contains("gothic/", StringComparison.OrdinalIgnoreCase))
        {
            gothic1Score++;
        }

        var cutscene = PathResolver.ResolveExisting(layout.WorkDirectory, "Data/Scripts/content/CUTSCENE");
        if (cutscene is not null)
        {
            if (PathResolver.FindChild(cutscene, "OU.BIN") is not null)
            {
                gothic1Score++;
            }

            if (PathResolver.FindChild(cutscene, "OU.DAT") is not null)
            {
                gothic2Score++;
            }
        }

        if (gothicIni.HasSection("KEYSDEFAULT1"))
        {
            gothic2Score++;
        }

        if (gothicIni.HasKey("GAME", "PATCHVERSION"))
        {
            gothic2Score++;
        }

        if (gothicIni.HasKey("GAME", "useGothic1Controls"))
        {
            gothic2Score++;
        }

        var detected = gothic1Score > gothic2Score
            ? GothicVersion.Gothic1
            : GothicVersion.Gothic2Classic;

        var patch = detected == GothicVersion.Gothic1
            ? 0
            : gothicIni.GetInt("GAME", "PATCHVERSION", 0);

        if (detected == GothicVersion.Gothic2Classic &&
            (patch >= 5 || HasAddonArchives(layout.DataDirectory)))
        {
            detected = GothicVersion.Gothic2NotR;
        }

        var source = "auto";

        switch (versionOverride)
        {
            case GameVersionOverride.Gothic1:
                detected = GothicVersion.Gothic1;
                patch = 0;
                source = "override -g1";
                break;
            case GameVersionOverride.Gothic2Classic:
                detected = GothicVersion.Gothic2Classic;
                patch = 0;
                source = "override -g2c";
                break;
            case GameVersionOverride.Gothic2NotR:
                detected = GothicVersion.Gothic2NotR;
                patch = Math.Max(patch, 5);
                source = "override -g2";
                break;
        }

        return new GothicVersionInfo(detected, patch, source);
    }

    private static bool HasAddonArchives(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            return false;
        }

        try
        {
            return Directory
                .EnumerateFiles(dataDirectory, "*.vdf", SearchOption.TopDirectoryOnly)
                .Any(path => Path.GetFileName(path).Contains("_Addon", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
