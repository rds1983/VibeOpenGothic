namespace VibeOpenGothic.Data.Installs;

/// <summary>
/// A validated Gothic installation: resolved directories, detected version,
/// optional mod manifest and the ordered archive list to mount.
/// </summary>
public sealed class GothicInstall
{
    public GothicInstall(
        GothicInstallLayout layout,
        string gothicIniPath,
        string scriptsDirectory,
        GothicVersionInfo version,
        ModManifest mod,
        IReadOnlyList<VdfArchive> archives,
        IReadOnlyList<string> warnings)
    {
        Layout = layout;
        GothicIniPath = gothicIniPath;
        ScriptsDirectory = scriptsDirectory;
        Version = version;
        Mod = mod;
        Archives = archives;
        Warnings = warnings;
    }

    public GothicInstallLayout Layout { get; }

    public string RootPath => Layout.RootPath;

    public string SystemDirectory => Layout.SystemDirectory;

    public string DataDirectory => Layout.DataDirectory;

    public string WorkDirectory => Layout.WorkDirectory;

    public string GothicIniPath { get; }

    public string ScriptsDirectory { get; }

    public GothicVersionInfo Version { get; }

    public ModManifest Mod { get; }

    public IReadOnlyList<VdfArchive> Archives { get; }

    public IReadOnlyList<string> Warnings { get; }

    public string DefaultGameDatName => Mod.GameDat ?? "GOTHIC.DAT";

    public string DefaultWorldName =>
        Mod.World ?? (Version.IsGothic1 ? "world.zen" : "newworld.zen");

    public override string ToString()
    {
        return RootPath;
    }
}
